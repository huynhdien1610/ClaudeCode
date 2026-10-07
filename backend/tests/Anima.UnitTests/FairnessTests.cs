using Anima.Fairness;

namespace Anima.UnitTests;

public class FairnessTests
{
    private const string Seed = "anima-demo-server-seed-001";

    [Fact(DisplayName = "SC-PF-01 vector kiểm thử chuẩn")]
    public void StandardVector()
    {
        var rolls = Enumerable.Range(0, 5).Select(i => FairnessMath.Roll(Seed, "keeper2049", 1, i)).ToArray();
        Assert.Equal([457142, 594361, 140124, 227524, 278885], rolls);
    }

    [Fact(DisplayName = "SC-PF-02 mã băm server seed")]
    public void SeedHash() =>
        Assert.Equal("9bda19bd88620c85d06a774f70f150a0379a20995f91a3525caf895375f6ccdf", FairnessMath.Sha256Hex(Seed));

    [Fact(DisplayName = "SC-PF-01 rarity của vector (tỷ lệ phương án A)")]
    public void StandardVectorRarities()
    {
        // 45/25/18/7/4/1 % → Common < 450000, Uncommon < 700000 ...
        (string, int)[] table = [("common", 450_000), ("uncommon", 250_000), ("rare", 180_000), ("epic", 70_000), ("legendary", 40_000), ("secret", 10_000)];
        var rarities = Enumerable.Range(0, 5).Select(i => FairnessMath.Pick(FairnessMath.Roll(Seed, "keeper2049", 1, i), table)).ToArray();
        Assert.Equal(["uncommon", "uncommon", "common", "common", "common"], rarities);
    }

    [Fact(DisplayName = "Giá trị quay luôn trong [0, 1,000,000) và phân bố đều")]
    public void RollsAreUniform()
    {
        var buckets = new int[10];
        for (var n = 1; n <= 20_000; n++)
            for (var s = 0; s < 5; s++)
            {
                var v = FairnessMath.Roll("seed", "client", n, s);
                Assert.InRange(v, 0, 999_999);
                buckets[v / 100_000]++;
            }
        // 100,000 mẫu / 10 nhóm = 10,000 mỗi nhóm; sai lệch 5% là rất rộng so với dao động ngẫu nhiên (~1%).
        Assert.All(buckets, b => Assert.InRange(b, 9_500, 10_500));
    }

    [Fact(DisplayName = "Chọn thẻ dùng message :c khác message rarity")]
    public void CardRollDiffersFromRarityRoll() =>
        Assert.NotEqual(FairnessMath.Roll(Seed, "keeper2049", 1, 0), FairnessMath.Roll(Seed, "keeper2049", 1, 0, "c"));
}
