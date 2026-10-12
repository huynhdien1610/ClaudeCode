using Anima.Arena;
using Xunit;

namespace Anima.UnitTests;

/// <summary>Glicko-2 (BR-PVP-06) và đồng hồ lượt (BR-BTL-08).</summary>
public class ArenaRulesTests
{
    private static readonly Glicko P = Glicko.Initial;

    [Fact(DisplayName = "Glicko-2: thắng tăng điểm, thua giảm điểm, độ lệch (rd) giảm sau mỗi ván, độ biến động giữ trong khoảng hợp lý")]
    public void WinLoseDirection()
    {
        var win = Glicko2.Update(P, P, 1); var loss = Glicko2.Update(P, P, 0);
        Assert.True(win.Rating > 1500 && loss.Rating < 1500);
        Assert.Equal(1500 - loss.Rating, win.Rating - 1500, 6);                         // đối thủ cùng điểm: dịch chuyển đối xứng
        Assert.True(win.Rd < P.Rd && loss.Rd < P.Rd);
        Assert.InRange(win.Vol, 0.01, 0.2);
    }

    [Fact(DisplayName = "Glicko-2: thắng đối thủ mạnh hơn được nhiều điểm hơn thắng đối thủ yếu hơn; điểm chắc (rd thấp) dịch ít hơn")]
    public void UpsetAndCertainty()
    {
        var strong = new Glicko(1800, 80, 0.06); var weak = new Glicko(1200, 80, 0.06);
        var me = new Glicko(1500, 80, 0.06);
        Assert.True(Glicko2.Update(me, strong, 1).Rating - 1500 > Glicko2.Update(me, weak, 1).Rating - 1500);
        var certain = Glicko2.Update(new Glicko(1500, 50, 0.06), P, 1).Rating - 1500;
        var unsure = Glicko2.Update(new Glicko(1500, 350, 0.06), P, 1).Rating - 1500;
        Assert.True(unsure > certain);
    }

    [Fact(DisplayName = "Glicko-2: ví dụ bài báo gốc một ván — người 1500/200 thắng đối thủ 1400/30 cho điểm gần 1500 + vài chục")]
    public void PaperScaleSanity()
    {
        var r = Glicko2.Update(new Glicko(1500, 200, 0.06), new Glicko(1400, 30, 0.06), 1);
        Assert.InRange(r.Rating, 1520, 1620); Assert.InRange(r.Rd, 150, 200);
    }

    [Fact(DisplayName = "Cửa sổ ghép trận rộng dần theo thời gian chờ")]
    public void MatchWindowWidens()
    {
        Assert.Equal(100, ArenaRules.Window(0)); Assert.Equal(100, ArenaRules.Window(-5));
        Assert.True(ArenaRules.Window(60) > ArenaRules.Window(10));
    }

    [Fact(DisplayName = "BR-BTL-08: hạn lượt = 20 giây + quỹ dự phòng; quỹ chỉ bị trừ phần vượt 20 giây, không âm")]
    public void TurnClock()
    {
        var t = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        Assert.Equal(t.AddSeconds(50), ArenaRules.Deadline(t, 30_000));
        Assert.Equal(30_000, ArenaRules.BankAfterTurn(30_000, TimeSpan.FromSeconds(12)));
        Assert.Equal(20_000, ArenaRules.BankAfterTurn(30_000, TimeSpan.FromSeconds(30)));
        Assert.Equal(0, ArenaRules.BankAfterTurn(5_000, TimeSpan.FromSeconds(60)));
    }
}
