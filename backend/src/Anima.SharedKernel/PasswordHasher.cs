using System.Globalization;
using System.Security.Cryptography;

namespace Anima.SharedKernel;

/// <summary>PBKDF2-HMAC-SHA256 có muối. Định dạng lưu: pbkdf2$vòng$muối$băm. Dùng chung cho người chơi và quản trị viên.</summary>
public static class PasswordHasher
{
    public static string Hash(string password, int iterations)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var h = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, 32);
        return $"pbkdf2${iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(h)}";
    }

    public static bool Verify(string password, string stored)
    {
        var p = stored.Split('$');
        if (p.Length != 4 || p[0] != "pbkdf2") return false;
        var h = Rfc2898DeriveBytes.Pbkdf2(password, Convert.FromBase64String(p[2]), int.Parse(p[1], CultureInfo.InvariantCulture), HashAlgorithmName.SHA256, 32);
        return CryptographicOperations.FixedTimeEquals(h, Convert.FromBase64String(p[3]));
    }
}
