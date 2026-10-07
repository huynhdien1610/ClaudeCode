using System.Net;
using System.Text.Json;
using Xunit;

namespace Anima.IntegrationTests;

/// <summary>Nhiệm vụ Tân thủ 7 ngày: SC-NEW-02..07 (BR-NEW-03/04/05).</summary>
public sealed class QuestTests(ApiFixture fx) : IClassFixture<ApiFixture>
{
    private async Task Age(Player p, int days) => await fx.Sql("UPDATE identity.account SET created_at = now() - make_interval(days => @d) WHERE id=@i", ("d", days), ("i", p.Id));
    private static JsonElement Day(JsonElement quests, int day) => quests.GetProperty("days").EnumerateArray().First(d => d.GetProperty("day").GetInt32() == day);
    private async Task<Guid> OpenNextUnopened(Player p, string kind)
    {
        var id = (await p.Get("/v1/me/packs")).Body.EnumerateArray().First(x => x.GetProperty("kind").GetString() == kind).GetProperty("id").GetGuid();
        var o = await p.Open(id); Assert.True(o.Ok, o.Body.ToString());
        return id;
    }

    [Fact(DisplayName = "SC-NEW-03: ngày 1 chưa mở pack thì chưa nhận; mở gói chào mừng rồi nhận 1 pack cơ bản gắn chặt tài khoản; nhận lần hai bị từ chối")]
    public async Task Day1ClaimGivesBasicPack()
    {
        var p = await fx.NewPlayer();
        var q = await p.Get("/v1/me/quests");
        Assert.Equal(1, q["currentDay"].GetInt32()); Assert.False(q["expired"].GetBoolean());
        Assert.True(Day(q.Body, 1).GetProperty("available").GetBoolean()); Assert.False(Day(q.Body, 1).GetProperty("available").GetBoolean() && Day(q.Body, 2).GetProperty("available").GetBoolean());
        Assert.Equal("QUEST_NOT_COMPLETE", (await p.Post("/v1/me/quests/1/claim")).Code);
        Assert.Equal("QUEST_NOT_AVAILABLE", (await p.Post("/v1/me/quests/2/claim")).Code);

        await p.OpenWelcomePackAsync();
        Assert.True(Day((await p.Get("/v1/me/quests")).Body, 1).GetProperty("complete").GetBoolean());
        var c = await p.Post("/v1/me/quests/1/claim");
        Assert.True(c.Ok, c.Body.ToString()); Assert.Equal("BASIC_PACK", c["rewardType"].GetString());
        var basic = (await p.Get("/v1/me/packs")).Body.EnumerateArray().Single(x => x.GetProperty("kind").GetString() == "basic");
        Assert.True(basic.GetProperty("soulbound").GetBoolean());
        Assert.Equal(HttpStatusCode.Conflict, (await p.Post("/v1/me/quests/1/claim")).Status);
        Assert.Equal("QUEST_ALREADY_CLAIMED", (await p.Post("/v1/me/quests/1/claim")).Code);
        Assert.True(Day((await p.Get("/v1/me/quests")).Body, 1).GetProperty("claimed").GetBoolean());
    }

    [Fact(DisplayName = "SC-NEW-04: làm bù ngày 2 vào ngày 4 và ngày 7 được; sang ngày 8 thì QUEST_EXPIRED; ngày chưa tới bị từ chối")]
    public async Task MakeUpAndExpiry()
    {
        var p = await fx.NewPlayer(); await Age(p, 3);                      // đang ở ngày 4
        Assert.Equal("QUEST_NOT_COMPLETE", (await p.Post("/v1/me/quests/2/claim")).Code);
        Assert.Equal("QUEST_NOT_AVAILABLE", (await p.Post("/v1/me/quests/5/claim")).Code);
        await p.VerifyPhone();
        Assert.True((await p.Post("/v1/me/quests/2/claim")).Ok);

        var late = await fx.NewPlayer(); await Age(late, 6);                // ngày 7: vẫn làm bù được
        await late.VerifyPhone();
        Assert.True((await late.Post("/v1/me/quests/2/claim")).Ok);

        var gone = await fx.NewPlayer(); await Age(gone, 7);                // ngày 8: hết hạn
        await gone.VerifyPhone();
        Assert.Equal("QUEST_EXPIRED", (await gone.Post("/v1/me/quests/2/claim")).Code);
        var q = await gone.Get("/v1/me/quests"); Assert.True(q["expired"].GetBoolean());
    }

    [Fact(DisplayName = "SC-NEW-03/05/07: đi hết 7 ngày — 5 pack cơ bản, 400 Coin ngày 6–7, đủ 30 thẻ gắn chặt, mỗi thẻ tối đa 2 bản")]
    public async Task FullSevenDays()
    {
        var p = await fx.NewPlayer(); await Age(p, 6);                      // ngày 7: mọi ngày đều đã tới
        var coinBefore = (await p.Balances()).Coin;
        await p.OpenWelcomePackAsync();
        Assert.True((await p.Post("/v1/me/quests/1/claim")).Ok);
        await p.VerifyPhone();
        Assert.True((await p.Post("/v1/me/quests/2/claim")).Ok);
        await OpenNextUnopened(p, "basic");                                  // 2 pack đã mở
        Assert.True((await p.Post("/v1/me/quests/3/claim")).Ok);
        await OpenNextUnopened(p, "basic");
        Assert.True((await p.Post("/v1/me/quests/4/claim")).Ok);
        await OpenNextUnopened(p, "basic");
        Assert.True((await p.Post("/v1/me/quests/5/claim")).Ok);
        await OpenNextUnopened(p, "basic"); await OpenNextUnopened(p, "basic");   // đủ 6 pack
        var d6 = await p.Post("/v1/me/quests/6/claim"); Assert.True(d6.Ok, d6.Body.ToString());
        Assert.Equal("COIN", d6["rewardType"].GetString()); Assert.Equal(200, d6["rewardCoin"].GetInt64());
        Assert.True((await p.Post("/v1/me/quests/7/claim")).Ok);
        Assert.Equal(coinBefore + 400, (await p.Balances()).Coin);

        var items = await p.Collection();
        Assert.Equal(30, items.Count);
        Assert.All(items, i => Assert.True(i.GetProperty("soulbound").GetBoolean()));
        Assert.All(items.GroupBy(i => i.GetProperty("card").GetProperty("id").GetInt32()), g => Assert.True(g.Count() <= 2, $"thẻ {g.Key} có {g.Count()} bản"));
        Assert.Empty((await p.Get("/v1/me/packs")).Body.EnumerateArray());
    }

    [Fact(DisplayName = "Nhiệm vụ: ngày không hợp lệ bị từ chối; cần đăng nhập")]
    public async Task Validation()
    {
        var p = await fx.NewPlayer();
        Assert.Equal("VALIDATION_FAILED", (await p.Post("/v1/me/quests/9/claim")).Code);
        Assert.Equal(HttpStatusCode.Unauthorized, (await new Player.Anon(fx).Get("/v1/me/quests")).Status);
    }
}
