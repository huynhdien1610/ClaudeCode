namespace Anima.Arena;

/// <summary>Điểm kỹ năng theo Glicko-2: rating, độ lệch (rd) và độ biến động (vol).</summary>
public readonly record struct Glicko(double Rating, double Rd, double Vol)
{
    public static readonly Glicko Initial = new(1500, 350, 0.06);
}

/// <summary>
/// Glicko-2 (Glickman 2013) cho một ván đấu đơn. <c>score</c> là 1 thắng, 0 thua. Hằng số hệ thống tau = 0,5.
/// Thuần tính toán, không phụ thuộc DB, nên test được bằng ví dụ trong bài báo gốc.
/// </summary>
public static class Glicko2
{
    private const double Tau = 0.5, Scale = 173.7178, Epsilon = 1e-6;

    private static double G(double phi) => 1 / Math.Sqrt(1 + 3 * phi * phi / (Math.PI * Math.PI));
    private static double E(double mu, double muJ, double phiJ) => 1 / (1 + Math.Exp(-G(phiJ) * (mu - muJ)));

    public static Glicko Update(Glicko player, Glicko opponent, double score)
    {
        var mu = (player.Rating - 1500) / Scale; var phi = player.Rd / Scale;
        var muJ = (opponent.Rating - 1500) / Scale; var phiJ = opponent.Rd / Scale;

        var g = G(phiJ); var e = E(mu, muJ, phiJ);
        var v = 1 / (g * g * e * (1 - e));
        var delta = v * g * (score - e);

        // Bước 5: tìm độ biến động mới bằng thuật toán Illinois.
        var a = Math.Log(player.Vol * player.Vol);
        double F(double x)
        {
            var ex = Math.Exp(x);
            return ex * (delta * delta - phi * phi - v - ex) / (2 * Math.Pow(phi * phi + v + ex, 2)) - (x - a) / (Tau * Tau);
        }
        var A = a; double B;
        if (delta * delta > phi * phi + v) B = Math.Log(delta * delta - phi * phi - v);
        else { var k = 1; while (F(a - k * Tau) < 0) k++; B = a - k * Tau; }
        var fA = F(A); var fB = F(B);
        while (Math.Abs(B - A) > Epsilon)
        {
            var C = A + (A - B) * fA / (fB - fA); var fC = F(C);
            if (fC * fB <= 0) { A = B; fA = fB; } else fA /= 2;
            B = C; fB = fC;
        }
        var sigma = Math.Exp(A / 2);

        var phiStar = Math.Sqrt(phi * phi + sigma * sigma);
        var phiNew = 1 / Math.Sqrt(1 / (phiStar * phiStar) + 1 / v);
        var muNew = mu + phiNew * phiNew * g * (score - e);
        return new Glicko(muNew * Scale + 1500, phiNew * Scale, sigma);
    }
}
