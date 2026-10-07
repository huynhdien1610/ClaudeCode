namespace Anima.SharedKernel;

public static class TimeZones
{
    /// <summary>
    /// Trình duyệt vẫn báo một số tên múi giờ cũ (Chromium trả "Asia/Saigon" cho Việt Nam) trong khi máy chủ có thể chỉ có tên chuẩn.
    /// Bảng này đổi tên cũ sang tên chuẩn IANA hiện hành.
    /// </summary>
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.Ordinal)
    {
        ["Asia/Saigon"] = "Asia/Ho_Chi_Minh",
        ["Asia/Calcutta"] = "Asia/Kolkata",
        ["Asia/Katmandu"] = "Asia/Kathmandu",
        ["Asia/Rangoon"] = "Asia/Yangon",
        ["Asia/Macao"] = "Asia/Macau",
        ["Asia/Thimbu"] = "Asia/Thimphu",
        ["Asia/Dacca"] = "Asia/Dhaka",
        ["Asia/Ujung_Pandang"] = "Asia/Makassar",
        ["Europe/Kiev"] = "Europe/Kyiv",
        ["America/Buenos_Aires"] = "America/Argentina/Buenos_Aires",
        ["Pacific/Samoa"] = "Pacific/Pago_Pago",
        ["Atlantic/Faeroe"] = "Atlantic/Faroe",
        ["Australia/Canberra"] = "Australia/Sydney",
        ["Asia/Chongqing"] = "Asia/Shanghai",
        ["Asia/Harbin"] = "Asia/Shanghai",
    };

    /// <summary>Tìm múi giờ theo tên IANA, chấp nhận tên cũ. Trả về false nếu không biết.</summary>
    public static bool TryResolve(string? id, out TimeZoneInfo tz)
    {
        tz = TimeZoneInfo.Utc;
        if (string.IsNullOrWhiteSpace(id)) return false;
        foreach (var candidate in new[] { id, Aliases.GetValueOrDefault(id) })
        {
            if (candidate is null) continue;
            try { tz = TimeZoneInfo.FindSystemTimeZoneById(candidate); return true; }
            catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException) { /* thử tên kế tiếp */ }
        }
        return false;
    }

    /// <summary>
    /// Khoảng [từ, đến) của "ngày" theo múi giờ cố định của tài khoản, trả về UTC (Npgsql chỉ nhận offset 0).
    /// Dùng cho hạn mức ngày (BR-WAL-06, BR-FRG-07). Múi giờ lạ thì dùng UTC.
    /// </summary>
    public static (DateTimeOffset From, DateTimeOffset To) LocalDayRange(DateTimeOffset now, string tzId)
    {
        if (!TryResolve(tzId, out var tz)) tz = TimeZoneInfo.Utc;
        var local = TimeZoneInfo.ConvertTime(now, tz);
        var startLocal = new DateTime(local.Year, local.Month, local.Day, 0, 0, 0, DateTimeKind.Unspecified);
        var endLocal = startLocal.AddDays(1);
        return (new DateTimeOffset(startLocal, tz.GetUtcOffset(startLocal)).ToUniversalTime(), new DateTimeOffset(endLocal, tz.GetUtcOffset(endLocal)).ToUniversalTime());
    }
}
