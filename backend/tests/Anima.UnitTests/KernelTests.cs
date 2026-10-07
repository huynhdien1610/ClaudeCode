using Anima.SharedKernel;

namespace Anima.UnitTests;

public class KernelTests
{
    [Fact(DisplayName = "BR-CHK-01: ngày theo múi giờ của tài khoản (Việt Nam UTC+7)")]
    public void LocalDayRangeInVietnam()
    {
        var now = DateTimeOffset.Parse("2026-10-06T20:30:00Z");                // 03:30 ngày 07/10 theo giờ VN
        var (from, to) = TimeZones.LocalDayRange(now, "Asia/Ho_Chi_Minh");
        Assert.Equal(DateTimeOffset.Parse("2026-10-06T17:00:00Z"), from);
        Assert.Equal(DateTimeOffset.Parse("2026-10-07T17:00:00Z"), to);
        Assert.Equal(TimeSpan.Zero, from.Offset);                                 // Npgsql chỉ nhận UTC
    }

    [Fact(DisplayName = "Múi giờ lạ dùng UTC")]
    public void UnknownTimezoneFallsBackToUtc()
    {
        var (from, to) = TimeZones.LocalDayRange(DateTimeOffset.Parse("2026-10-06T20:30:00Z"), "Mars/Olympus");
        Assert.Equal(DateTimeOffset.Parse("2026-10-06T00:00:00Z"), from);
        Assert.Equal(TimeSpan.FromDays(1), to - from);
    }

    [Theory(DisplayName = "Trình duyệt báo tên múi giờ cũ (Chromium: Asia/Saigon) vẫn được chấp nhận")]
    [InlineData("Asia/Saigon", "Asia/Ho_Chi_Minh")]
    [InlineData("Asia/Calcutta", "Asia/Kolkata")]
    [InlineData("Europe/Kiev", "Europe/Kyiv")]
    [InlineData("Asia/Ho_Chi_Minh", "Asia/Ho_Chi_Minh")]
    public void TimezoneAliasesResolve(string reported, string expectedId)
    {
        Assert.True(TimeZones.TryResolve(reported, out var tz));
        Assert.Equal(TimeZoneInfo.FindSystemTimeZoneById(expectedId).BaseUtcOffset, tz.BaseUtcOffset);
    }

    [Theory(DisplayName = "Múi giờ không tồn tại bị từ chối")]
    [InlineData("Mars/Olympus")]
    [InlineData("")]
    [InlineData(null)]
    public void UnknownTimezoneRejected(string? id) => Assert.False(TimeZones.TryResolve(id, out _));

    [Fact(DisplayName = "FieldCipher: mã hóa khứ hồi, mỗi lần mã hóa khác nhau, bản mã bị sửa thì từ chối")]
    public void FieldCipherRoundTrip()
    {
        var c = new FieldCipher(new byte[32], new byte[32]);
        var a = c.Encrypt("user@example.com"); var b = c.Encrypt("user@example.com");
        Assert.Equal("user@example.com", c.Decrypt(a));
        Assert.NotEqual(a, b);                                                    // nonce ngẫu nhiên
        Assert.Equal(c.LookupHash("x"), c.LookupHash("x"));                       // hash tra cứu thì ổn định
        a[^1] ^= 1;
        Assert.ThrowsAny<System.Security.Cryptography.CryptographicException>(() => c.Decrypt(a));
    }

    [Fact(DisplayName = "FieldCipher từ chối khóa sai độ dài")]
    public void FieldCipherKeySize() => Assert.Throws<ArgumentException>(() => new FieldCipher(new byte[16], new byte[32]));
}
