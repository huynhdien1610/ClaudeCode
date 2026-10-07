using Anima.Fairness;

namespace Anima.UnitTests;

/// <summary>SC-PACK-16 / NFR-12: tỷ lệ thực tế khớp tỷ lệ công bố trong khoảng tin cậy 99% trên ≥ 1 triệu lượt.</summary>
public class OddsStatisticsTests
{
    private static readonly (string Rarity, int Ppm)[] Odds = [("common", 450_000), ("uncommon", 250_000), ("rare", 180_000), ("epic", 70_000), ("legendary", 40_000), ("secret", 10_000)];

    [Fact(DisplayName = "SC-PACK-16: 1,000,000 lượt quay khớp tỷ lệ công bố (khoảng tin cậy 99%)")]
    public void MillionRollsMatchPublishedOdds()
    {
        const int n = 1_000_000;
        var counts = new Dictionary<string, int>();
        for (var i = 0; i < n; i++)
        {
            var roll = FairnessMath.Roll("statistical-seed", "client", i / 5 + 1, i % 5);
            var r = FairnessMath.Pick(roll, Odds);
            counts[r] = counts.GetValueOrDefault(r) + 1;
        }
        foreach (var (rarity, ppm) in Odds)
        {
            var p = ppm / 1_000_000.0;
            var sd = Math.Sqrt(n * p * (1 - p));
            Assert.InRange(counts[rarity], n * p - 2.576 * sd, n * p + 2.576 * sd);     // z = 2.576 ứng với 99%
        }
    }

    [Fact(DisplayName = "Pick: ranh giới bảng tỷ lệ cộng dồn")]
    public void PickBoundaries()
    {
        Assert.Equal("common", FairnessMath.Pick(0, Odds));
        Assert.Equal("common", FairnessMath.Pick(449_999, Odds));
        Assert.Equal("uncommon", FairnessMath.Pick(450_000, Odds));
        Assert.Equal("secret", FairnessMath.Pick(999_999, Odds));
        Assert.Equal("legendary", FairnessMath.Pick(990_000 - 1, Odds));
    }
}
