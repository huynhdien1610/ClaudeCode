using System.Net;
using System.Text.Json;
using Xunit;

namespace Anima.IntegrationTests;

/// <summary>Đấu trường xếp hạng PvP: ghép trận, Glicko-2, Arena Point, đồng hồ lượt, giới hạn cặp (BR-PVP-03/06, BR-BTL-07/08).</summary>
public sealed class ArenaTests(ApiFixture fx) : IClassFixture<ApiFixture>
{
    private async Task<(Player P, Guid Deck)> Fighter()
    {
        var p = await fx.NewPlayer(); await p.BuyAndOpen(7); await p.BuyAndOpen(7);
        var ids = DeckTests.PickValid(await p.Collection()); Assert.NotNull(ids);
        var d = await p.Post("/v1/decks", new { name = "Đấu", cardInstanceIds = ids });
        Assert.True(d["valid"].GetBoolean(), d.Body.ToString());
        return (p, d["id"].GetGuid());
    }

    /// <summary>Hai người vào hàng đợi; người đợi trước được coi như đã chờ lâu để cửa sổ điểm đủ rộng.</summary>
    private async Task<Guid> Pair((Player P, Guid Deck) a, (Player P, Guid Deck) b)
    {
        var ja = await a.P.Post("/v1/arena/queue", new { deckId = a.Deck }); Assert.True(ja.Ok, ja.Body.ToString()); Assert.True(ja["queued"].GetBoolean());
        await fx.Sql("UPDATE arena.queue SET joined_at = now() - interval '1 hour' WHERE account_id=@a", ("a", a.P.Id));
        var jb = await b.P.Post("/v1/arena/queue", new { deckId = b.Deck }); Assert.True(jb.Ok, jb.Body.ToString());
        Assert.False(jb["queued"].GetBoolean()); return jb["matchId"].GetGuid();
    }

    private static Task<Resp> Act(Player p, Guid id, string type) => p.Post($"/v1/arena/matches/{id}/actions", new { type });

    private async Task<Guid> Finish((Player P, Guid Deck) a, (Player P, Guid Deck) b)
    {
        var id = await Pair(a, b);
        Assert.True((await Act(a.P, id, "keep")).Ok); Assert.True((await Act(b.P, id, "keep")).Ok);
        var v = await a.P.Get($"/v1/arena/matches/{id}");
        var loser = v["yourTurn"].GetBoolean() ? a.P : b.P;                                              // người đang tới lượt đầu hàng: người kia thắng
        Assert.True((await Act(loser, id, "concede")).Ok);
        return id;
    }

    [Fact(DisplayName = "Ghép trận xếp hạng: hai người vào hàng đợi được ghép, đối thủ bị giấu tay và bộ bài, đầu hàng cho kết quả, Glicko-2 và AP cập nhật, có lịch sử, xếp hạng và phát lại")]
    public async Task QueueMatchAndFinish()
    {
        await fx.Sql("DELETE FROM arena.queue");
        var a = await Fighter(); var b = await Fighter();
        var id = await Pair(a, b);

        var o = await a.P.Get("/v1/arena"); Assert.Equal(id, o["activeMatchId"].GetGuid()); Assert.Equal(1500, o["rating"].GetInt32()); Assert.Equal(JsonValueKind.Null, o["queue"].ValueKind);
        var va = await a.P.Get($"/v1/arena/matches/{id}"); var vb = await b.P.Get($"/v1/arena/matches/{id}");
        Assert.Equal("mulligan", va["phase"].GetString()); Assert.Equal(5, va["you"].GetProperty("hand").GetArrayLength()); Assert.Equal(5, va["opponent"].GetProperty("hand").GetInt32());
        Assert.DoesNotContain("\"seed\"", va.Body.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(va["opponentName"].GetString(), vb["opponentName"].GetString());
        Assert.Equal("INVALID_STATE", (await a.P.Get($"/v1/arena/matches/{id}/replay")).Code);          // chưa xong thì chưa có phát lại
        Assert.Equal(HttpStatusCode.NotFound, (await (await fx.NewPlayer()).Get($"/v1/arena/matches/{id}")).Status);   // người ngoài không thấy trận
        Assert.Equal("MATCH_IN_PROGRESS", (await a.P.Post("/v1/arena/queue", new { deckId = a.Deck })).Code);

        Assert.True((await Act(a.P, id, "keep")).Ok); Assert.True((await Act(b.P, id, "keep")).Ok);
        Assert.Equal("NOT_ALLOWED_IN_RANKED", (await a.P.Post($"/v1/arena/matches/{id}/actions", new { type = "memory", id = "1", slot = 0 })).Code);
        var cur = await a.P.Get($"/v1/arena/matches/{id}");
        var (mover, other) = cur["yourTurn"].GetBoolean() ? (a.P, b.P) : (b.P, a.P);
        Assert.Equal("NOT_YOUR_TURN", (await Act(other, id, "end")).Code);
        var done = await Act(mover, id, "concede");
        Assert.True(done.Ok); Assert.Equal("Finished", done["status"].GetString()); Assert.False(done["result"].GetProperty("won").GetBoolean());
        var r = done["rating"]; Assert.True(r.GetProperty("delta").GetDouble() < 0); Assert.Equal(0, r.GetProperty("ap").GetInt32());

        var winner = mover == a.P ? b.P : a.P;
        var wo = await winner.Get("/v1/arena"); var lo = await mover.Get("/v1/arena");
        Assert.True(wo["rating"].GetInt32() > 1500 && lo["rating"].GetInt32() < 1500);
        Assert.Equal(10, wo["ap"].GetInt64()); Assert.Equal(0, lo["ap"].GetInt64());
        Assert.Equal(JsonValueKind.Null, wo["activeMatchId"].ValueKind);
        var hist = await winner.Get("/v1/arena/matches"); var h0 = hist.Body.EnumerateArray().First();
        Assert.True(h0.GetProperty("won").GetBoolean()); Assert.Equal(10, h0.GetProperty("ap").GetInt32());
        Assert.NotEmpty((await winner.Get("/v1/arena/leaderboard")).Body.EnumerateArray());
        var rep = await winner.Get($"/v1/arena/matches/{id}/replay");
        Assert.True(rep.Ok); Assert.Equal(rep["result"].GetProperty("winner").GetInt32(), rep["replayed"].GetProperty("winner").GetInt32());   // server phát lại cho cùng kết quả
        Assert.Equal(HttpStatusCode.Conflict, (await Act(mover, id, "end")).Status);                     // đã kết thúc
    }

    [Fact(DisplayName = "BR-BTL-08: hết giờ được xử lý lười khi có người đọc trận — lượt tự kết thúc, 3 lượt hết giờ liên tiếp thì thua; kết quả vẫn phát lại đúng")]
    public async Task TimeoutsAreSettled()
    {
        await fx.Sql("DELETE FROM arena.queue");
        var a = await Fighter(); var b = await Fighter(); var id = await Pair(a, b);
        Assert.True((await Act(a.P, id, "keep")).Ok); Assert.True((await Act(b.P, id, "keep")).Ok);
        await fx.Sql("UPDATE arena.match SET turn_started_at = now() - interval '30 minutes' WHERE id=@i", ("i", id));
        var v = await a.P.Get($"/v1/arena/matches/{id}");
        Assert.Equal("Finished", v["status"].GetString()); Assert.Equal("TIMEOUT", v["result"].GetProperty("reason").GetString());
        var rep = await a.P.Get($"/v1/arena/matches/{id}/replay");
        Assert.Equal(rep["result"].GetProperty("winner").GetInt32(), rep["replayed"].GetProperty("winner").GetInt32());
        Assert.Contains(rep["actions"].EnumerateArray(), x => x.GetProperty("type").GetString() == "timeout");
    }

    [Fact(DisplayName = "Không chọn tay đầu trong 20 giây thì máy chủ tự giữ tay; ghép trận khi điểm chênh quá cửa sổ thì chưa ghép")]
    public async Task MulliganTimeoutAndWindow()
    {
        await fx.Sql("DELETE FROM arena.queue");
        var a = await Fighter(); var b = await Fighter(); var id = await Pair(a, b);
        await fx.Sql("UPDATE arena.match SET created_at = now() - interval '1 minute', turn_started_at = now() WHERE id=@i", ("i", id));
        var v = await a.P.Get($"/v1/arena/matches/{id}"); Assert.Equal("main", v["phase"].GetString());

        await Act(a.P, id, "concede");
        var c = await Fighter(); var d = await Fighter();
        await fx.Sql("INSERT INTO arena.rating(account_id,rating,rd,vol,games,wins,updated_at) VALUES(@a,2400,60,0.06,50,40,now()) ON CONFLICT (account_id) DO UPDATE SET rating=2400", ("a", c.P.Id));
        Assert.True((await c.P.Post("/v1/arena/queue", new { deckId = c.Deck })).Ok);
        var jd = await d.P.Post("/v1/arena/queue", new { deckId = d.Deck });
        Assert.True(jd["queued"].GetBoolean());                                                           // 2400 và 1500 cách nhau quá xa, chưa ghép
        Assert.Equal(HttpStatusCode.NoContent, (await d.P.Anonymous(HttpMethod.Delete, "/v1/arena/queue")).Status);
        Assert.Equal(HttpStatusCode.NoContent, (await c.P.Anonymous(HttpMethod.Delete, "/v1/arena/queue")).Status);
        Assert.Equal(JsonValueKind.Null, (await d.P.Get("/v1/arena"))["queue"].ValueKind);
    }

    [Fact(DisplayName = "BR-PVP-06: mỗi cặp tối đa 3 trận xếp hạng mỗi ngày — trận thứ 4 không được ghép")]
    public async Task PairDailyLimit()
    {
        await fx.Sql("DELETE FROM arena.queue");
        var a = await Fighter(); var b = await Fighter();
        for (var i = 0; i < 3; i++) await Finish(a, b);
        Assert.True((await a.P.Post("/v1/arena/queue", new { deckId = a.Deck })).Ok);
        await fx.Sql("UPDATE arena.queue SET joined_at = now() - interval '1 hour' WHERE account_id=@a", ("a", a.P.Id));
        var j = await b.P.Post("/v1/arena/queue", new { deckId = b.Deck });
        Assert.True(j["queued"].GetBoolean(), "trận thứ 4 cùng cặp trong ngày không được ghép");
        await a.P.Anonymous(HttpMethod.Delete, "/v1/arena/queue"); await b.P.Anonymous(HttpMethod.Delete, "/v1/arena/queue");
    }

    [Fact(DisplayName = "Vào hàng đợi cần bộ bài hợp lệ (BR-DECK-07) và tài khoản không bị hạn chế")]
    public async Task QueueNeedsValidDeckAndGoodStanding()
    {
        var p = await fx.NewPlayer(); await p.BuyAndOpen(1);
        var d = await p.Post("/v1/decks", new { name = "Dở", cardInstanceIds = Array.Empty<Guid>() });
        Assert.Equal("DECK_INVALID", (await p.Post("/v1/arena/queue", new { deckId = d["id"].GetGuid() })).Code);
        Assert.Equal("VALIDATION_FAILED", (await p.Post("/v1/arena/queue", new { })).Code);
        var f = await Fighter();
        await fx.Sql("UPDATE identity.account SET status='Restricted', restriction_reason='FRAUD' WHERE id=@i", ("i", f.P.Id));
        Assert.Equal("ACCOUNT_RESTRICTED", (await f.P.Post("/v1/arena/queue", new { deckId = f.Deck })).Code);
    }
}
