using System.Net;
using System.Text.Json;
using Anima.Memory;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Anima.IntegrationTests;

/// <summary>Ký ức phong ấn: BR-CARD-05, BR-CARD-08, BR-CARD-09 (SC-SKL-04/05).</summary>
public sealed class MemoryTests(ApiFixture fx) : IClassFixture<ApiFixture>
{
    private static async Task<int> PassiveDef(Player p) => (await p.Get("/v1/memories"))["available"].EnumerateArray().First().GetProperty("defId").GetInt32();

    private async Task Grant(Guid acc, int def)
    {
        await using var scope = fx.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IMemoryApi>().GrantAsync(acc, def, default);
    }

    [Fact(DisplayName = "Tổng quan: mỗi Anima giữ Passive có một loại Ký ức phong ấn (24 loại), kho trống, kho tối đa 5, được mua tuần này")]
    public async Task Overview()
    {
        var p = await fx.NewPlayer(); var o = await p.Get("/v1/memories");
        Assert.True(o.Ok); Assert.Equal(24, o["available"].GetArrayLength()); Assert.Equal(0, o["total"].GetInt32());
        Assert.Equal(5, o["max"].GetInt32()); Assert.True(o["canBuyThisWeek"].GetBoolean());
        Assert.All(o["available"].EnumerateArray(), x => Assert.NotEqual("", x.GetProperty("skill").GetString()));
    }

    [Fact(DisplayName = "SC-SKL-05: mua bằng Gem trừ giá, vào kho; lần hai cùng tuần bị WEEKLY_LIMIT_REACHED; thẻ không có Passive bị từ chối")]
    public async Task BuyWithGemOncePerWeek()
    {
        var p = await fx.NewPlayer(); await p.TopUp(500); var def = await PassiveDef(p);
        var before = (await p.Balances()).Gem;
        var b = await p.Post("/v1/memories/purchase", new { cardDefinitionId = def });
        Assert.True(b.Ok, b.Body.ToString()); Assert.Equal(1, b["total"].GetInt32()); Assert.False(b["canBuyThisWeek"].GetBoolean());
        Assert.Equal(before - 50, (await p.Balances()).Gem);
        var again = await p.Post("/v1/memories/purchase", new { cardDefinitionId = def });
        Assert.Equal("WEEKLY_LIMIT_REACHED", again.Code); Assert.Equal(HttpStatusCode.Conflict, again.Status);
        Assert.Equal(before - 50, (await p.Balances()).Gem);                                       // lỗi không trừ thêm Gem
        await fx.Sql("DELETE FROM memory.weekly_purchase WHERE account_id=@a", ("a", p.Id));
        Assert.Equal("VALIDATION_FAILED", (await p.Post("/v1/memories/purchase", new { cardDefinitionId = 1 })).Code);      // thẻ id 1 là Anima không giữ Passive
    }

    [Fact(DisplayName = "Mua bằng Gem khi không đủ Gem: INSUFFICIENT_BALANCE và không ghi giới hạn tuần")]
    public async Task BuyNeedsGem()
    {
        var p = await fx.NewPlayer(); var def = await PassiveDef(p);
        await fx.Sql("UPDATE wallet.balance SET amount = 0 WHERE account_id=@a AND currency='GEM'", ("a", p.Id));
        Assert.Equal("INSUFFICIENT_BALANCE", (await p.Post("/v1/memories/purchase", new { cardDefinitionId = def })).Code);
        Assert.True((await p.Get("/v1/memories"))["canBuyThisWeek"].GetBoolean());
    }

    [Fact(DisplayName = "Kho tối đa 5: nhận thêm từ Story/sự kiện/Battle Pass bị MEMORY_STOCK_FULL; mua Gem khi đầy cũng không trừ Gem")]
    public async Task StockCap()
    {
        var p = await fx.NewPlayer(); await p.TopUp(500); var def = await PassiveDef(p);
        for (var i = 0; i < 5; i++) await Grant(p.Id, def);
        await Assert.ThrowsAsync<Anima.SharedKernel.DomainException>(() => Grant(p.Id, def));
        var before = (await p.Balances()).Gem;
        Assert.Equal("MEMORY_STOCK_FULL", (await p.Post("/v1/memories/purchase", new { cardDefinitionId = def })).Code);
        Assert.Equal(before, (await p.Balances()).Gem);
        Assert.True((await p.Get("/v1/memories"))["canBuyThisWeek"].GetBoolean());                  // rollback cả dòng giới hạn tuần
    }

    [Fact(DisplayName = "Ký ức phong ấn không giao dịch được: không có Card Instance trong bộ sưu tập, trận luyện tập chốt kho lúc vào trận")]
    public async Task BattleSnapshot()
    {
        var p = await fx.NewPlayer(); await p.BuyAndOpen(7); await p.BuyAndOpen(7);
        var ids = DeckTests.PickValid(await p.Collection()); Assert.NotNull(ids);
        var deck = (await p.Post("/v1/decks", new { name = "K", cardInstanceIds = ids }))["id"].GetGuid();
        var def = await PassiveDef(p); await Grant(p.Id, def); await Grant(p.Id, def);

        var v = await p.Post("/v1/battles/practice", new { deckId = deck });
        Assert.True(v.Ok, v.Body.ToString());
        var mem = v["you"].GetProperty("memories").EnumerateArray().Single();
        Assert.Equal(def, mem.GetProperty("defId").GetInt32()); Assert.Equal(2, mem.GetProperty("qty").GetInt32());
        Assert.True(v["you"].GetProperty("memoryReady").GetBoolean());
        var id = v["id"].GetGuid();
        Assert.True((await p.Post($"/v1/battles/{id}/actions", new { type = "keep" })).Ok);

        // Không có Anima cùng hệ trên sân thì không dùng được và không mất thẻ.
        var bad = await p.Post($"/v1/battles/{id}/actions", new { type = "memory", id = def.ToString(), slot = 0 });
        Assert.Equal("NO_MATCHING_ELEMENT", bad.Code);
        Assert.Equal(2, (await p.Get("/v1/memories"))["total"].GetInt32());
    }
}
