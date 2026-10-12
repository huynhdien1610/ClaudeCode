using Anima.Market;
using Xunit;

namespace Anima.UnitTests;

public class MarketRulesTests
{
    [Theory(DisplayName = "SC-MKT-01 / BR-MKT-05: phí 5% làm tròn lên đến đơn vị nguyên")]
    [InlineData(1000, 50)]
    [InlineData(999, 50)]
    [InlineData(10, 1)]
    [InlineData(1, 1)]
    [InlineData(100, 5)]
    [InlineData(101, 6)]
    [InlineData(2345, 118)]
    public void Fee(long price, long fee) => Assert.Equal(fee, MarketRules.Fee(price));

    [Theory(DisplayName = "SC-MKT-10 / BR-MKT-04: khoảng giá thẻ Rare 100–50.000 Coin; Gem = Coin ÷ 10")]
    [InlineData("rare", "COIN", 100, 50_000)]
    [InlineData("rare", "GEM", 10, 5_000)]
    [InlineData("common", "COIN", 10, 5_000)]
    [InlineData("common", "GEM", 1, 500)]
    public void Ranges(string rarity, string currency, long min, long max) => Assert.Equal((min, max), MarketRules.Range(rarity, currency));

    [Fact(DisplayName = "Mọi rarity đều có khoảng giá hợp lệ (min ≥ 1, min < max) và tăng dần theo độ hiếm")]
    public void RangesAreSane()
    {
        long prevMax = 0;
        foreach (var r in Anima.Contracts.Rarities.Order)
        {
            var (min, max) = MarketRules.Range(r, "COIN");
            Assert.True(min >= 1 && min < max); Assert.True(max > prevMax); prevMax = max;
        }
    }
}
