using System.Globalization;

namespace Textil.Planning.Services;

/// <summary>
/// The database stores UTC; users enter and read times in the company time zone (config "App:TimeZone"),
/// independent of the server's own zone.
/// </summary>
public sealed class LocalTime(TimeZoneInfo zone)
{
    public DateTime ToLocal(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone);

    public DateTime ToUtc(DateTime local)
    {
        local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(local)) // skipped hour at the DST switch
            local = local.AddHours(1);
        return TimeZoneInfo.ConvertTimeToUtc(local, zone);
    }

    public DateTime Now(TimeProvider clock) => ToLocal(clock.GetUtcNow().UtcDateTime);

    public string Format(DateTime utc) => ToLocal(utc).ToString("dd.MM.yyyy. HH:mm", CultureInfo.InvariantCulture);

    public string FormatDate(DateTime utc) => ToLocal(utc).ToString("dd.MM.yyyy.", CultureInfo.InvariantCulture);
}
