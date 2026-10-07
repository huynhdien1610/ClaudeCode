using Anima.Quest;
using Xunit;

namespace Anima.UnitTests;

public class QuestDayTests
{
    private static readonly DateTimeOffset Created = new(2026, 10, 6, 15, 0, 0, TimeSpan.Zero);   // 22:00 ngày 6/10 giờ Việt Nam

    [Theory(DisplayName = "Ngày Tân thủ tính theo ngày lịch ở múi giờ tài khoản, không theo 24 giờ")]
    [InlineData("2026-10-06T16:59:00Z", "Asia/Ho_Chi_Minh", 1)]    // 23:59 cùng ngày 6/10 giờ VN
    [InlineData("2026-10-06T17:00:00Z", "Asia/Ho_Chi_Minh", 2)]    // 00:00 ngày 7/10 giờ VN → ngày 2 dù mới cách 2 giờ
    [InlineData("2026-10-06T17:00:00Z", "UTC", 1)]
    [InlineData("2026-10-12T17:00:00Z", "Asia/Ho_Chi_Minh", 8)]   // 13/10 giờ VN
    [InlineData("2026-10-12T16:59:00Z", "Asia/Ho_Chi_Minh", 7)]   // 23:59 ngày 12/10 vẫn ngày 7
    [InlineData("2026-10-06T17:00:00Z", "Mars/Olympus", 1)]       // múi giờ lạ → UTC
    public void CurrentDay(string now, string tz, int expected) =>
        Assert.Equal(expected, NewbieQuests.CurrentDay(Created, DateTimeOffset.Parse(now), tz));

    [Fact(DisplayName = "Không bao giờ nhỏ hơn 1, và có đúng 7 ngày với ngày 1–5 thưởng pack, ngày 6–7 thưởng 200 Coin")]
    public void Definitions()
    {
        Assert.Equal(1, NewbieQuests.CurrentDay(Created, Created.AddHours(-5), "UTC"));
        Assert.Equal(7, NewbieQuests.All.Length);
        Assert.All(NewbieQuests.All.Where(d => d.Day <= 5), d => Assert.Equal("BASIC_PACK", d.RewardType));
        Assert.All(NewbieQuests.All.Where(d => d.Day >= 6), d => { Assert.Equal("COIN", d.RewardType); Assert.Equal(200, d.RewardCoin); });
    }
}
