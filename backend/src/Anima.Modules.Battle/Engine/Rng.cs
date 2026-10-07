using System.Security.Cryptography;

namespace Anima.Battle.Engine;

/// <summary>
/// Bộ sinh số tất định từ một seed (HMAC-SHA256 theo bộ đếm, lấy mẫu loại bỏ để không lệch). Cùng seed và cùng chuỗi lời gọi
/// luôn cho cùng kết quả — nền tảng của phát lại trận (BR-BTL-10). Không dùng System.Random.
/// </summary>
public sealed class DeterministicRng(byte[] seed)
{
    private long _counter;
    private byte[] _buf = [];
    private int _pos;

    private uint NextUInt32()
    {
        if (_pos + 4 > _buf.Length) { _buf = HMACSHA256.HashData(seed, BitConverter.GetBytes(_counter++)); _pos = 0; }
        var v = BitConverter.ToUInt32(_buf, _pos); _pos += 4;
        return v;
    }

    /// <summary>Số nguyên đều trong [0, n).</summary>
    public int Next(int n)
    {
        if (n <= 0) throw new ArgumentOutOfRangeException(nameof(n));
        var limit = uint.MaxValue - (uint.MaxValue % (uint)n) - 1;   // bỏ vùng đuôi để mọi giá trị đều xác suất như nhau
        uint v;
        do v = NextUInt32(); while (v > limit);
        return (int)(v % (uint)n);
    }

    /// <summary>Xáo Fisher–Yates tại chỗ.</summary>
    public void Shuffle<T>(IList<T> list)
    {
        for (var i = list.Count - 1; i > 0; i--) { var j = Next(i + 1); (list[i], list[j]) = (list[j], list[i]); }
    }
}
