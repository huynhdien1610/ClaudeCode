using System.Security.Cryptography;
using System.Text;

namespace Anima.Fairness;

/// <summary>
/// Thuật toán quay có thể kiểm chứng (BR-PF-01/02, SAD 8.1). Hàm thuần: ai cũng tính lại được từ server seed đã công bố.
/// </summary>
public static class FairnessMath
{
    public const uint Limit = 4_294_000_000u;      // floor(2^32 / 1e6) * 1e6, loại bỏ thiên lệch modulo
    public const int Range = 1_000_000;

    public static string Sha256Hex(string s) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(s)));

    /// <summary>Giá trị quay trong [0, 1,000,000) cho (seed, nonce, slot). <paramref name="suffix"/> = "c" khi chọn Card Definition.</summary>
    public static int Roll(string serverSeed, string clientSeed, int nonce, int slot, string? suffix = null)
    {
        var key = Encoding.UTF8.GetBytes(serverSeed);
        var baseMsg = $"{clientSeed}:{nonce}:{slot}" + (suffix is null ? "" : ":" + suffix);
        for (var attempt = 0; ; attempt++)
        {
            var msg = attempt == 0 ? baseMsg : $"{baseMsg}:{attempt}";
            var digest = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(msg));
            for (var i = 0; i < 32; i += 4)
            {
                var u = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(digest.AsSpan(i, 4));
                if (u < Limit) return (int)(u % Range);
            }
        }
    }

    /// <summary>Tra giá trị quay vào bảng tỷ lệ cộng dồn (phần triệu) theo thứ tự rarity.</summary>
    public static T Pick<T>(int roll, IReadOnlyList<(T Item, int Ppm)> table)
    {
        var acc = 0;
        foreach (var (item, ppm) in table) { acc += ppm; if (roll < acc) return item; }
        return table[^1].Item;
    }
}
