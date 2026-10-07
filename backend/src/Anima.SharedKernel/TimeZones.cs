namespace Anima.SharedKernel;

public static class TimeZones
{
    /// <summary>
    /// Khoảng [từ, đến) của "ngày" theo múi giờ cố định của tài khoản, trả về UTC (Npgsql chỉ nhận offset 0).
    /// Dùng cho hạn mức ngày (BR-WAL-06, BR-FRG-07). Múi giờ lạ thì dùng UTC.
    /// </summary>
    public static (DateTimeOffset From, DateTimeOffset To) LocalDayRange(DateTimeOffset now, string tzId)
    {
        TimeZoneInfo tz;
        try { tz = TimeZoneInfo.FindSystemTimeZoneById(tzId); } catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException) { tz = TimeZoneInfo.Utc; }
        var local = TimeZoneInfo.ConvertTime(now, tz);
        var startLocal = new DateTime(local.Year, local.Month, local.Day, 0, 0, 0, DateTimeKind.Unspecified);
        var endLocal = startLocal.AddDays(1);
        return (new DateTimeOffset(startLocal, tz.GetUtcOffset(startLocal)).ToUniversalTime(), new DateTimeOffset(endLocal, tz.GetUtcOffset(endLocal)).ToUniversalTime());
    }
}
