using System.Text;
using Nop.Core;
using Nop.Plugin.Widgets.MarketLocator.Domain;
using Nop.Services.Helpers;

namespace Nop.Plugin.Widgets.MarketLocator.Services;

public interface IIcsBuilder
{
    string BuildForLocation(MarketLocation location, string storeUrl);
    string BuildForAll(IEnumerable<MarketLocation> locations, string storeUrl);
}

/// <summary>
/// Builds RFC 5545-compliant iCalendar (.ics) content for market location dates.
/// Injected as a scoped service so IDateTimeHelper (which reads the store's
/// configured timezone from nopCommerce settings) is available at build time.
/// </summary>
public class IcsBuilder : IIcsBuilder
{
    private readonly IDateTimeHelper _dateTimeHelper;

    public IcsBuilder(IDateTimeHelper dateTimeHelper)
    {
        _dateTimeHelper = dateTimeHelper;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public string BuildForLocation(MarketLocation location, string storeUrl)
    {
        var tzId = GetIanaTimeZoneId(_dateTimeHelper.DefaultStoreTimeZone);
        var dates = ParseDates(location.UpcomingDates, DateTime.UtcNow.Year);
        var events = dates.Select(d => BuildEvent(location, d, storeUrl, tzId));
        return WrapCalendar(events, $"{Escape(location.Name)} — Market Dates", tzId);
    }

    public string BuildForAll(IEnumerable<MarketLocation> locations, string storeUrl)
    {
        var tzId = GetIanaTimeZoneId(_dateTimeHelper.DefaultStoreTimeZone);
        var events = locations.SelectMany(loc =>
        {
            var dates = ParseDates(loc.UpcomingDates, DateTime.UtcNow.Year);
            return dates.Select(d => BuildEvent(loc, d, storeUrl, tzId));
        });
        return WrapCalendar(events, "All Market Locations", tzId);
    }

    // ── Internals ─────────────────────────────────────────────────────────────

    private static string BuildEvent(MarketLocation loc, DateTime date, string storeUrl, string tzId)
    {
        var (hasTime, startDt, endDt) = TryParseHours(loc.Hours, date);

        var uid = $"market-{loc.Id}-{date:yyyyMMdd}@{new Uri(storeUrl).Host}";
        var now = loc.LastModifiedUtc.ToString("yyyyMMddTHHmmssZ");
        var mapsUrl = $"https://www.google.com/maps/search/?api=1&query={Uri.EscapeDataString(loc.Address)}";
        var description = $"Market: {loc.Name}\\nHours: {loc.Hours}\\nAddress: {loc.Address}\\n" +
                          $"Directions: {mapsUrl}\\nMore markets: {storeUrl}market-locations";

        var sb = new StringBuilder();
        sb.AppendLine("BEGIN:VEVENT");
        sb.AppendLine($"UID:{uid}");
        sb.AppendLine($"DTSTAMP:{now}");
        sb.AppendLine($"SUMMARY:{Escape(loc.Name)} — {loc.City} Market");

        if (hasTime)
        {
            sb.AppendLine($"DTSTART;TZID={tzId}:{startDt:yyyyMMddTHHmmss}");
            sb.AppendLine($"DTEND;TZID={tzId}:{endDt:yyyyMMddTHHmmss}");
        }
        else
        {
            sb.AppendLine($"DTSTART;VALUE=DATE:{date:yyyyMMdd}");
            sb.AppendLine($"DTEND;VALUE=DATE:{date.AddDays(1):yyyyMMdd}");
        }

        sb.AppendLine($"LOCATION:{Escape(loc.Address)}");
        sb.AppendLine($"DESCRIPTION:{description}");
        sb.AppendLine($"URL:{storeUrl}market-locations");
        sb.AppendLine($"GEO:{loc.Latitude};{loc.Longitude}");

        var rrule = BuildRRule(loc.Frequency, date);
        if (rrule is not null)
            sb.AppendLine($"RRULE:{rrule}");

        sb.AppendLine("COLOR:green");
        sb.Append("END:VEVENT");
        return sb.ToString();
    }

    private static string WrapCalendar(IEnumerable<string> events, string calName, string tzId)
    {
        var sb = new StringBuilder();
        sb.AppendLine("BEGIN:VCALENDAR");
        sb.AppendLine("VERSION:2.0");
        sb.AppendLine("PRODID:-//MarketLocator//NopCommerce Plugin//EN");
        sb.AppendLine("CALSCALE:GREGORIAN");
        sb.AppendLine("METHOD:PUBLISH");
        sb.AppendLine($"X-WR-CALNAME:{Escape(calName)}");
        sb.AppendLine($"X-WR-TIMEZONE:{tzId}");
        sb.AppendLine("X-PUBLISHED-TTL:PT1H");

        sb.AppendLine("BEGIN:VTIMEZONE");
        sb.AppendLine("TZID:Europe/London");
        sb.AppendLine("BEGIN:DAYLIGHT");
        sb.AppendLine("TZOFFSETFROM:+0000");
        sb.AppendLine("TZOFFSETTO:+0100");
        sb.AppendLine("TZNAME:BST");
        sb.AppendLine("DTSTART:19700329T010000");
        sb.AppendLine("RRULE:FREQ=YEARLY;BYMONTH=3;BYDAY=-1SU");
        sb.AppendLine("END:DAYLIGHT");
        sb.AppendLine("BEGIN:STANDARD");
        sb.AppendLine("TZOFFSETFROM:+0100");
        sb.AppendLine("TZOFFSETTO:+0000");
        sb.AppendLine("TZNAME:GMT");
        sb.AppendLine("DTSTART:19701025T020000");
        sb.AppendLine("RRULE:FREQ=YEARLY;BYMONTH=10;BYDAY=-1SU");
        sb.AppendLine("END:STANDARD");
        sb.AppendLine("END:VTIMEZONE");

        foreach (var ev in events)
            sb.AppendLine(ev);

        sb.Append("END:VCALENDAR");
        return FoldLines(sb.ToString());
    }

    // ── Timezone conversion ───────────────────────────────────────────────────

    /// <summary>
    /// RFC 5545 X-WR-TIMEZONE requires an IANA ID ("America/Chicago"), not a
    /// Windows ID ("Central Standard Time").
    ///
    /// IANA IDs never contain spaces; Windows IDs typically do — that is the
    /// cheapest heuristic to detect which kind we have.
    ///
    /// TimeZoneInfo.TryConvertWindowsIdToIanaId is built into .NET 6+ with no
    /// additional NuGet package required.
    /// </summary>
    private static string GetIanaTimeZoneId(TimeZoneInfo tz)
    {
        if (!tz.Id.Contains(' '))
            return tz.Id; // already IANA

        if (TimeZoneInfo.TryConvertWindowsIdToIanaId(tz.Id, out var ianaId))
            return ianaId;

        return "UTC"; // safe fallback — better than emitting an invalid value
    }

    // ── RRULE ─────────────────────────────────────────────────────────────────

    private static string? BuildRRule(string frequency, DateTime firstDate) =>
        frequency.ToLowerInvariant() switch
        {
            "weekly"    => "FREQ=WEEKLY;COUNT=52",
            "bi-weekly" => "FREQ=WEEKLY;INTERVAL=2;COUNT=26",
            "monthly"   => $"FREQ=MONTHLY;BYDAY={NthWeekdayRule(firstDate)};COUNT=12",
            _           => null
        };

    private static string NthWeekdayRule(DateTime date)
    {
        var dayAbbr = date.DayOfWeek switch
        {
            DayOfWeek.Monday    => "MO",
            DayOfWeek.Tuesday   => "TU",
            DayOfWeek.Wednesday => "WE",
            DayOfWeek.Thursday  => "TH",
            DayOfWeek.Friday    => "FR",
            DayOfWeek.Saturday  => "SA",
            _                   => "SU"
        };
        return $"{(int)Math.Ceiling(date.Day / 7.0)}{dayAbbr}";
    }

    // ── Hours parsing ─────────────────────────────────────────────────────────

    private static (bool success, DateTime start, DateTime end) TryParseHours(string hours, DateTime date)
    {
        if (string.IsNullOrWhiteSpace(hours)) return (false, date, date);

        var normalised = hours.Replace("\u2013", "-").Replace("\u2014", "-")
                              .Replace(" to ", "-", StringComparison.OrdinalIgnoreCase);
        var parts = normalised.Split('-', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2) return (false, date, date);

        if (DateTime.TryParse($"{date:yyyy-MM-dd} {parts[0]}", out var start) &&
            DateTime.TryParse($"{date:yyyy-MM-dd} {parts[1]}", out var end))
            return (true, start, end);

        return (false, date, date);
    }

    // ── Date parsing ──────────────────────────────────────────────────────────

    private static IEnumerable<DateTime> ParseDates(string pipeDates, int assumedYear) =>
        pipeDates.Split('|', StringSplitOptions.RemoveEmptyEntries)
                 .Select(d => d.Trim())
                 .Select(d => TryParseLooseDate(d, assumedYear, out var dt) ? (DateTime?)dt : null)
                 .Where(d => d.HasValue)
                 .Select(d => d!.Value);

    private static bool TryParseLooseDate(string raw, int assumedYear, out DateTime result)
    {
        var cleaned = System.Text.RegularExpressions.Regex.Replace(raw, @"^\w+,\s*", "");
        return DateTime.TryParse($"{cleaned} {assumedYear}", out result)
            || DateTime.TryParse(cleaned, out result);
    }

    // ── RFC 5545 helpers ──────────────────────────────────────────────────────

    private static string Escape(string value) =>
        value.Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,").Replace("\n", "\\n");

    private static string FoldLines(string input)
    {
        const int maxLineBytes = 75;
        var result = new StringBuilder();

        foreach (var line in input.Split('\n'))
        {
            var trimmed = line.TrimEnd('\r');
            var bytes = Encoding.UTF8.GetBytes(trimmed);

            if (bytes.Length <= maxLineBytes) { result.AppendLine(trimmed); continue; }

            var pos = 0; var first = true;
            while (pos < bytes.Length)
            {
                var take = Math.Min(first ? maxLineBytes : maxLineBytes - 1, bytes.Length - pos);
                while (take > 0 && (bytes[pos + take - 1] & 0xC0) == 0x80) take--;
                result.AppendLine(first ? Encoding.UTF8.GetString(bytes, pos, take)
                                        : " " + Encoding.UTF8.GetString(bytes, pos, take));
                pos += take; first = false;
            }
        }
        return result.ToString().TrimEnd();
    }
}
