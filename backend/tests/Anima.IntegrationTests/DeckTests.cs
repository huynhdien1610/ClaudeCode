using System.Net;
using System.Text.Json;
using Xunit;

namespace Anima.IntegrationTests;

/// <summary>Bộ bài: BR-DECK-01 → 07 (đường API; luật thuần có test đơn vị riêng).</summary>
public sealed class DeckTests(ApiFixture fx) : IClassFixture<ApiFixture>
{
    /// <summary>Chọn tham lam 30 thẻ thỏa luật từ bộ sưu tập; null nếu không đủ.</summary>
    private static List<Guid>? PickValid(IEnumerable<JsonElement> items)
    {
        var picked = new List<Guid>(); var support = 0; var perDef = new Dictionary<int, int>(); var perRarity = new Dictionary<string, int>();
        foreach (var i in items)
        {
            var card = i.GetProperty("card"); var def = card.GetProperty("id").GetInt32(); var rarity = card.GetProperty("rarity").GetString()!;
            var cap = rarity is "legendary" or "secret" ? 1 : 2;
            var rcap = rarity switch { "epic" => 4, "legendary" => 2, "secret" => 1, _ => 99 };
            var isSupport = card.GetProperty("type").GetString() != "anima";
            if ((isSupport && support >= 6) || perDef.GetValueOrDefault(def) >= cap || perRarity.GetValueOrDefault(rarity) >= rcap) continue;
            perDef[def] = perDef.GetValueOrDefault(def) + 1; perRarity[rarity] = perRarity.GetValueOrDefault(rarity) + 1;
            if (isSupport) support++;
            picked.Add(i.GetProperty("id").GetGuid());
            if (picked.Count == 30) return picked;
        }
        return null;
    }

    private async Task<(Player P, List<Guid> Ids)> Rich()
    {
        var p = await fx.NewPlayer(); await p.BuyAndOpen(7); await p.BuyAndOpen(7);
        var ids = PickValid(await p.Collection());
        Assert.NotNull(ids);
        return (p, ids!);
    }

    private static string[] Rules(Resp r) => r["violations"].EnumerateArray().Select(v => v.GetProperty("rule").GetString()!).ToArray();

    [Fact(DisplayName = "BR-DECK-01..05: lưu bộ dở dang (không hợp lệ), bổ sung đủ 30 lá thì hợp lệ, đặt mặc định, xóa")]
    public async Task BuildAndManage()
    {
        var (p, ids) = await Rich();
        var created = await p.Post("/v1/decks", new { name = "Bộ thử", cardInstanceIds = ids.Take(10) });
        Assert.Equal(HttpStatusCode.Created, created.Status);
        Assert.False(created["valid"].GetBoolean()); Assert.Contains("DECK_SIZE", Rules(created)); Assert.Equal(10, created["count"].GetInt32());
        var id = created["id"].GetGuid();

        var full = await p.Put($"/v1/decks/{id}", new { name = "Bộ chính", cardInstanceIds = ids });
        Assert.True(full.Ok, full.Body.ToString()); Assert.True(full["valid"].GetBoolean(), full.Body.ToString()); Assert.Equal(30, full["count"].GetInt32()); Assert.Equal("Bộ chính", full["name"].GetString());

        var def = await p.Post($"/v1/decks/{id}/default"); Assert.True(def["isDefault"].GetBoolean());
        var second = await p.Post("/v1/decks", new { name = "Bộ hai" });
        await p.Post($"/v1/decks/{second["id"].GetGuid()}/default");
        var list = (await p.Get("/v1/decks")).Body.EnumerateArray().ToList();
        Assert.Equal(1, list.Count(d => d.GetProperty("isDefault").GetBoolean()));           // chỉ một bộ mặc định
        Assert.Equal(HttpStatusCode.NoContent, (await p.Anonymous(HttpMethod.Delete, $"/v1/decks/{id}")).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await p.Get($"/v1/decks/{id}")).Status);
    }

    [Fact(DisplayName = "BR-DECK-05: tối đa 10 bộ mỗi tài khoản; tên 1–40 ký tự")]
    public async Task DeckLimitAndName()
    {
        var p = await fx.NewPlayer();
        for (var i = 0; i < 10; i++) Assert.True((await p.Post("/v1/decks", new { name = $"d{i}" })).Ok);
        Assert.Equal("DECK_LIMIT_REACHED", (await p.Post("/v1/decks", new { name = "d10" })).Code);
        var q = await fx.NewPlayer();
        Assert.Equal("VALIDATION_FAILED", (await q.Post("/v1/decks", new { name = "" })).Code);
        Assert.Equal("VALIDATION_FAILED", (await q.Post("/v1/decks", new { name = new string('x', 41) })).Code);
    }

    [Fact(DisplayName = "BR-DECK-04: chỉ dùng thẻ Owned của chính mình; thẻ người khác, thẻ đang niêm yết, trùng Instance, quá 30 lá đều bị từ chối")]
    public async Task OnlyOwnedCards()
    {
        var p = await fx.NewPlayer(); await p.BuyAndOpen(2);
        var mine = (await p.Collection()).Select(i => i.GetProperty("id").GetGuid()).ToList();
        var other = await fx.NewPlayer(); await other.BuyAndOpen(1);
        var theirs = (await other.Collection()).First().GetProperty("id").GetGuid();
        Assert.Equal("CARD_NOT_IN_ACCOUNT", (await p.Post("/v1/decks", new { name = "x", cardInstanceIds = new[] { mine[0], theirs } })).Code);
        Assert.Equal("CARD_NOT_IN_ACCOUNT", (await p.Post("/v1/decks", new { name = "x", cardInstanceIds = new[] { Guid.NewGuid() } })).Code);
        Assert.Equal("VALIDATION_FAILED", (await p.Post("/v1/decks", new { name = "x", cardInstanceIds = new[] { mine[0], mine[0] } })).Code);
        Assert.Equal("VALIDATION_FAILED", (await p.Post("/v1/decks", new { name = "x", cardInstanceIds = Enumerable.Range(0, 31).Select(_ => Guid.NewGuid()) })).Code);

        await fx.Sql("UPDATE collection.card_instance SET state='Listed' WHERE id=@i", ("i", mine[1]));
        Assert.Equal("CARD_LOCKED", (await p.Post("/v1/decks", new { name = "x", cardInstanceIds = new[] { mine[1] } })).Code);
        Assert.Equal(HttpStatusCode.NotFound, (await other.Get($"/v1/decks/{Guid.NewGuid()}")).Status);
    }

    [Fact(DisplayName = "BR-DECK-06: thẻ rời trạng thái Owned (bị rèn, niêm yết) thì bị gỡ khỏi bộ và bộ thành chưa hợp lệ")]
    public async Task CardsLeaveDeck()
    {
        var (p, ids) = await Rich();
        var d = await p.Post("/v1/decks", new { name = "Bộ chính", cardInstanceIds = ids });
        Assert.True(d["valid"].GetBoolean()); var id = d["id"].GetGuid();

        await fx.Sql("UPDATE collection.card_instance SET state='Listed' WHERE id=@i", ("i", ids[0]));
        await fx.Sql("UPDATE collection.card_instance SET state='Burned' WHERE id=@i", ("i", ids[1]));
        var after = await p.Get($"/v1/decks/{id}");
        Assert.False(after["valid"].GetBoolean()); Assert.Equal(28, after["count"].GetInt32());
        Assert.DoesNotContain(ids[0], after["cardInstanceIds"].EnumerateArray().Select(x => x.GetGuid()));
        Assert.Contains("DECK_SIZE", Rules(after));
        // Bổ sung lại thẻ khác thì hợp lệ trở lại (nếu còn thẻ thỏa luật)
        var refill = PickValid((await p.Collection()).Where(i => i.GetProperty("state").GetString() == "Owned"));
        Assert.NotNull(refill);
        Assert.True((await p.Put($"/v1/decks/{id}", new { name = "Bộ chính", cardInstanceIds = refill })).Ok);
    }

    [Fact(DisplayName = "Bộ bài của người khác không xem/sửa/xóa/đặt mặc định được; cần đăng nhập")]
    public async Task Isolation()
    {
        var a = await fx.NewPlayer(); var b = await fx.NewPlayer();
        var id = (await a.Post("/v1/decks", new { name = "của A" }))["id"].GetGuid();
        Assert.Equal(HttpStatusCode.NotFound, (await b.Get($"/v1/decks/{id}")).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await b.Put($"/v1/decks/{id}", new { name = "hack" })).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await b.Anonymous(HttpMethod.Delete, $"/v1/decks/{id}")).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await b.Post($"/v1/decks/{id}/default")).Status);
        Assert.Empty((await b.Get("/v1/decks")).Body.EnumerateArray());
        Assert.Equal(HttpStatusCode.Unauthorized, (await new Player.Anon(fx).Get("/v1/decks")).Status);
    }
}
