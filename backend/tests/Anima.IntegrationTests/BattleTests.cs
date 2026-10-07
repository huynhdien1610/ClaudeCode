using System.Net;
using System.Text.Json;
using Xunit;

namespace Anima.IntegrationTests;

/// <summary>Trận luyện tập với máy: BR-DECK-07, BR-BTL-02/07/10, SC-BTL-11/12 (đường API; luật có test đơn vị riêng).</summary>
public sealed class BattleTests(ApiFixture fx) : IClassFixture<ApiFixture>
{
    private async Task<(Player P, Guid DeckId)> PlayerWithDeck()
    {
        var p = await fx.NewPlayer(); await p.BuyAndOpen(7); await p.BuyAndOpen(7);
        var ids = DeckTests.PickValid(await p.Collection());
        Assert.NotNull(ids);
        var d = await p.Post("/v1/decks", new { name = "Chiến", cardInstanceIds = ids });
        Assert.True(d["valid"].GetBoolean(), d.Body.ToString());
        return (p, d["id"].GetGuid());
    }

    private static Task<Resp> Act(Player p, Guid id, string type, int? hand = null, int? slot = null, int? target = null) =>
        p.Post($"/v1/battles/{id}/actions", new { type, hand, slot, target });

    /// <summary>Chơi tự động phía người chơi: ra Anima đắt nhất được, đánh bằng mọi Anima đánh được, hết thì kết thúc lượt.</summary>
    private static async Task<Resp> PlayOut(Player p, Guid id, Resp view)
    {
        for (var guard = 0; guard < 400 && view["status"].GetString() == "InProgress"; guard++)
        {
            Assert.Equal("main", view["phase"].GetString()); Assert.True(view["yourTurn"].GetBoolean());
            var you = view["you"]; var opp = view["opponent"];
            var field = you.GetProperty("field").EnumerateArray().ToList(); var oppField = opp.GetProperty("field").EnumerateArray().ToList();
            var energy = you.GetProperty("energy").GetInt32();
            var free = field.FindIndex(f => f.ValueKind == JsonValueKind.Null);
            var play = you.GetProperty("hand").EnumerateArray().Where(c => c.GetProperty("type").GetString() == "anima" && c.GetProperty("cost").GetInt32() <= energy)
                .OrderByDescending(c => c.GetProperty("cost").GetInt32()).Select(c => (int?)c.GetProperty("index").GetInt32()).FirstOrDefault();
            Resp next;
            if (free >= 0 && play is not null) next = await Act(p, id, "play", hand: play, slot: free);
            else
            {
                var attacker = field.FindIndex(f => f.ValueKind != JsonValueKind.Null && f.GetProperty("canAttack").GetBoolean());
                if (attacker >= 0)
                {
                    var target = oppField.FindIndex(f => f.ValueKind != JsonValueKind.Null);
                    next = await Act(p, id, "attack", slot: attacker, target: target);      // -1 = Keeper khi sân đối phương trống
                }
                else next = await Act(p, id, "end");
            }
            Assert.True(next.Ok, next.Body.ToString());
            view = next;
        }
        return view;
    }

    [Fact(DisplayName = "BR-DECK-07: không vào trận bằng bộ chưa hợp lệ, bộ của người khác hoặc thiếu bộ")]
    public async Task StartRequiresValidDeck()
    {
        var p = await fx.NewPlayer();
        var partial = (await p.Post("/v1/decks", new { name = "dở" }))["id"].GetGuid();
        Assert.Equal("DECK_INVALID", (await p.Post("/v1/battles/practice", new { deckId = partial })).Code);
        Assert.Equal("VALIDATION_FAILED", (await p.Post("/v1/battles/practice", new { })).Code);
        var (_, deck) = await PlayerWithDeck();
        Assert.Equal(HttpStatusCode.NotFound, (await p.Post("/v1/battles/practice", new { deckId = deck })).Status);
        Assert.Equal(HttpStatusCode.Unauthorized, (await new Player.Anon(fx).Get("/v1/battles")).Status);
    }

    [Fact(DisplayName = "Bắt đầu trận: tay 5 lá thấy được, tay/bộ bài đối thủ bị giấu, không lộ seed; chỉ một trận đang đánh; đổi tay một lần (SC-BTL-11)")]
    public async Task StartAndMulligan()
    {
        var (p, deck) = await PlayerWithDeck();
        var v = await p.Post("/v1/battles/practice", new { deckId = deck });
        Assert.Equal(HttpStatusCode.Created, v.Status); var id = v["id"].GetGuid();
        Assert.Equal("mulligan", v["phase"].GetString()); Assert.True(v["you"].GetProperty("mulliganAvailable").GetBoolean());
        Assert.Equal(5, v["you"].GetProperty("hand").GetArrayLength());
        Assert.Equal(5, v["opponent"].GetProperty("hand").GetInt32());
        Assert.Equal(8000, v["you"].GetProperty("keeper").GetInt32());
        Assert.DoesNotContain("seed", v.Body.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("MATCH_IN_PROGRESS", (await p.Post("/v1/battles/practice", new { deckId = deck })).Code);
        Assert.Equal("INVALID_STATE", (await p.Get($"/v1/battles/{id}/replay")).Code);          // chưa kết thúc thì chưa có phát lại

        var m = await Act(p, id, "mulligan");
        Assert.True(m.Ok, m.Body.ToString()); Assert.Equal("main", m["phase"].GetString()); Assert.True(m["yourTurn"].GetBoolean());
        Assert.Equal("MULLIGAN_USED", (await Act(p, id, "mulligan")).Code);
        Assert.Equal("INVALID_ACTION", (await Act(p, id, "play", hand: 99, slot: 0)).Code);
        Assert.Equal("INVALID_ACTION", (await Act(p, id, "attack", slot: 0, target: -1)).Code);
        Assert.Equal("INVALID_ACTION", (await Act(p, id, "dance")).Code);
    }

    [Fact(DisplayName = "Chơi trọn một trận với máy qua API: kết thúc có kết quả, danh sách ghi nhận, phát lại khớp (BR-BTL-10, SC-BTL-12)")]
    public async Task FullMatchAndReplay()
    {
        var (p, deck) = await PlayerWithDeck();
        var v = await p.Post("/v1/battles/practice", new { deckId = deck }); var id = v["id"].GetGuid();
        v = await Act(p, id, "keep"); Assert.Equal("main", v["phase"].GetString());
        var done = await PlayOut(p, id, v);
        Assert.Equal("Finished", done["status"].GetString());
        var won = done["result"].GetProperty("won").GetBoolean(); var reason = done["result"].GetProperty("reason").GetString();
        Assert.Contains(reason, new[] { "KEEPER_DOWN", "DECK_OUT", "SUDDEN_DEATH" });

        Assert.Equal("MATCH_FINISHED", (await Act(p, id, "end")).Code);
        var replay = await p.Get($"/v1/battles/{id}/replay");
        Assert.True(replay.Ok, replay.Body.ToString());
        Assert.Equal(64, replay["seed"].GetString()!.Length);
        Assert.Equal(won, replay["replayed"].GetProperty("won").GetBoolean()); Assert.Equal(reason, replay["replayed"].GetProperty("reason").GetString());
        Assert.Equal(30, replay["playerDeck"].GetArrayLength()); Assert.Equal(30, replay["opponentDeck"].GetArrayLength());
        var list = (await p.Get("/v1/battles")).Body.EnumerateArray().First();
        Assert.Equal("Finished", list.GetProperty("status").GetString()); Assert.Equal(won, list.GetProperty("won").GetBoolean());
        Assert.True((await p.Post("/v1/battles/practice", new { deckId = deck })).Ok);        // xong trận cũ thì mở được trận mới
    }

    [Fact(DisplayName = "Đầu hàng: trận kết thúc, người chơi thua; người khác không xem/đánh được trận này")]
    public async Task ConcedeAndIsolation()
    {
        var (p, deck) = await PlayerWithDeck();
        var id = (await p.Post("/v1/battles/practice", new { deckId = deck }))["id"].GetGuid();
        var other = await fx.NewPlayer();
        Assert.Equal(HttpStatusCode.NotFound, (await other.Get($"/v1/battles/{id}")).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await Act(other, id, "concede")).Status);
        var c = await Act(p, id, "concede");
        Assert.True(c.Ok, c.Body.ToString());
        Assert.False(c["result"].GetProperty("won").GetBoolean()); Assert.Equal("CONCEDE", c["result"].GetProperty("reason").GetString());
        Assert.True((await p.Get($"/v1/battles/{id}/replay")).Ok);
    }
}
