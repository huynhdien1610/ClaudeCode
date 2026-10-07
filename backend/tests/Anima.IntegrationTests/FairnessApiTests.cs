using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace Anima.IntegrationTests;

public sealed class FairnessApiTests(ApiFixture fx) : IClassFixture<ApiFixture>
{
    private static string Sha256(string s) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(s)));

    [Fact(DisplayName = "SC-PF-01: công cụ kiểm chứng công khai cho đúng vector chuẩn")]
    public async Task VerifyEndpointStandardVector()
    {
        var r = await new Player.Anon(fx).Post("/v1/fairness/verify", new { serverSeed = "anima-demo-server-seed-001", clientSeed = "keeper2049", nonce = 1, slots = 5 });
        Assert.Equal([457142, 594361, 140124, 227524, 278885], r["rolls"].EnumerateArray().Select(x => x.GetInt32()).ToArray());
        Assert.Equal("9bda19bd88620c85d06a774f70f150a0379a20995f91a3525caf895375f6ccdf", r["serverSeedHash"].GetString());
    }

    [Fact(DisplayName = "SC-PF-02: mã băm hiển thị trước lần quay đầu và không lộ server seed")]
    public async Task HashShownBeforeFirstRoll()
    {
        var p = await fx.NewPlayer();
        var f = await p.Get("/v1/fairness");
        Assert.Equal(64, f["serverSeedHash"].GetString()!.Length);
        Assert.Equal(1, f["nextNonce"].GetInt32());
        Assert.DoesNotContain("serverSeed\"", f.Body.ToString());
    }

    [Fact(DisplayName = "SC-PF-04: xin seed đang dùng bị từ chối SEED_NOT_REVEALED")]
    public async Task ActiveSeedNeverRevealed()
    {
        var p = await fx.NewPlayer();
        var r = await p.Get("/v1/fairness/server-seed");
        Assert.Equal(HttpStatusCode.Forbidden, r.Status);
        Assert.Equal("SEED_NOT_REVEALED", r.Code);
    }

    [Fact(DisplayName = "SC-PF-03: đổi seed công bố seed cũ khớp mã băm, seed mới nonce về 1, client seed mới")]
    public async Task RotateRevealsOldSeed()
    {
        var p = await fx.NewPlayer(); await p.TopUp(100);
        var before = await p.Get("/v1/fairness");
        var open = await p.Open((await p.Buy(1))[0]);
        Assert.True(open.Ok);
        Assert.Equal(2, (await p.Get("/v1/fairness"))["nextNonce"].GetInt32());

        var rot = await p.Post("/v1/fairness/rotate", new { clientSeed = "my-lucky-seed" });
        var prev = rot["previous"];
        Assert.Equal(before["serverSeedHash"].GetString(), prev.GetProperty("serverSeedHash").GetString());
        Assert.Equal(prev.GetProperty("serverSeedHash").GetString(), Sha256(prev.GetProperty("serverSeed").GetString()!));
        Assert.Equal(1, prev.GetProperty("noncesUsed").GetInt32());
        var cur = rot["current"];
        Assert.Equal(1, cur.GetProperty("nextNonce").GetInt32());
        Assert.Equal("my-lucky-seed", cur.GetProperty("clientSeed").GetString());
        Assert.NotEqual(before["serverSeedHash"].GetString(), cur.GetProperty("serverSeedHash").GetString());
        Assert.Single((await p.Get("/v1/fairness/history")).Body.EnumerateArray());
    }

    [Theory(DisplayName = "Validation client seed")]
    [InlineData("")]
    [InlineData("has:colon")]
    public async Task ClientSeedValidation(string seed)
    {
        var p = await fx.NewPlayer();
        Assert.Equal("VALIDATION_FAILED", (await p.Put("/v1/fairness/client-seed", new { clientSeed = seed })).Code);
    }

    [Fact(DisplayName = "BR-PF-01: người chơi đặt client seed; nonce giữ nguyên")]
    public async Task ClientSeedCanBeChanged()
    {
        var p = await fx.NewPlayer();
        var r = await p.Put("/v1/fairness/client-seed", new { clientSeed = "keeper2049" });
        Assert.Equal("keeper2049", r["clientSeed"].GetString());
        Assert.Equal(1, r["nextNonce"].GetInt32());
    }

    [Fact(DisplayName = "SC-PF-05: đổi seed và mở pack đồng thời — lần mở dùng trọn một seed, không trộn")]
    public async Task RotateAndOpenConcurrently()
    {
        for (var round = 0; round < 10; round++)
        {
            var p = await fx.NewPlayer(); await p.TopUp(100);
            var pack = (await p.Buy(1))[0];
            var oldHash = (await p.Get("/v1/fairness"))["serverSeedHash"].GetString();
            var open = p.Open(pack); var rot = p.Post("/v1/fairness/rotate");
            await Task.WhenAll(open, rot);
            Assert.True((await open).Ok); Assert.True((await rot).Ok);
            var newHash = (await rot)["current"].GetProperty("serverSeedHash").GetString();
            var used = (await p.Get($"/v1/pack-instances/{pack}/opening"))["fairness"].GetProperty("seedHash").GetString();
            Assert.Contains(used, new[] { oldHash, newHash });
            // Nếu dùng seed cũ thì nonce cũ là 1; nếu dùng seed mới thì seed mới đã dùng nonce 1 → nonce kế tiếp là 2.
            var nonce = (await p.Get($"/v1/pack-instances/{pack}/opening"))["fairness"].GetProperty("nonce").GetInt32();
            Assert.Equal(1, nonce);
        }
    }
}
