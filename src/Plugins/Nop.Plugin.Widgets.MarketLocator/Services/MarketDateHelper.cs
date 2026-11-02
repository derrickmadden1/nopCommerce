namespace Nop.Plugin.Widgets.MarketLocator.Services;

/// <summary>
/// Shared helpers for parsing and filtering the loosely-formatted date strings
/// stored in <see cref="Domain.MarketLocation.UpcomingDates"/>.
///
/// Dates are stored as pipe-delimited human-readable strings such as
/// "Sat, Mar 14|Sat, Mar 21". This utility parses them and filters to
/// only those that are today or in the future, so past dates are never
/// surfaced in the checkout selector or the public map.
/// </summary>
public static class MarketDateHelper
{
    /// <summary>
    /// Parses a single loose date string against an assumed year.
    /// Strips a leading day-of-week ("Sat, ") before parsing, and if a
    /// day-of-week was specified, validates that the parsed date matches it.
    /// Tries "Mar 14 2025" first, then bare "Mar 14" as fallback.
    /// </summary>
    public static bool TryParseDate(string raw, int assumedYear, out DateTime result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var trimmed = raw.Trim();
        DayOfWeek? expectedDayOfWeek = null;

        var dayMatch = System.Text.RegularExpressions.Regex.Match(trimmed, @"^([A-Za-z]+),\s*");
        if (dayMatch.Success && TryParseDayOfWeek(dayMatch.Groups[1].Value, out var dow))
        {
            expectedDayOfWeek = dow;
        }

        var cleaned = System.Text.RegularExpressions.Regex.Replace(
            trimmed, @"^\w+,\s*", "");

        cleaned = System.Text.RegularExpressions.Regex.Replace(
            cleaned, @"(?<=\b\d+)(st|nd|rd|th)\b", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        var parsed = DateTime.TryParse($"{cleaned} {assumedYear}", out result)
            || DateTime.TryParse(cleaned, out result);

        if (!parsed)
            return false;

        if (expectedDayOfWeek.HasValue && result.DayOfWeek != expectedDayOfWeek.Value)
            return false;

        return true;
    }

    /// <summary>
    /// Resolves a loose date string to a today-or-future <see cref="DateTime"/> relative to <paramref name="asOf"/>.
    /// Handles Nov/Dec rollover into Jan/Feb/Mar of the following year while respecting any day-of-week prefix.
    /// </summary>
    public static bool TryResolveFutureDate(string raw, DateTime asOf, out DateTime resolvedDate)
    {
        var today = asOf.Date;
        var year = today.Year;

        if (TryParseDate(raw, year, out var dt) && dt.Date >= today)
        {
            resolvedDate = dt.Date;
            return true;
        }

        // Handle year-boundary: "Jan 5" in November/December should be read as
        // next year if it is a valid Jan/Feb/Mar date in the following year.
        if (today.Month >= 11 &&
            TryParseDate(raw, year + 1, out var nextYearDt) &&
            nextYearDt.Month <= 3 &&
            nextYearDt.Date >= today)
        {
            resolvedDate = nextYearDt.Date;
            return true;
        }

        resolvedDate = default;
        return false;
    }

    private static bool TryParseDayOfWeek(string token, out DayOfWeek dayOfWeek)
    {
        switch (token.Trim().ToLowerInvariant())
        {
            case "mon":
            case "monday":
                dayOfWeek = DayOfWeek.Monday;
                return true;
            case "tue":
            case "tues":
            case "tuesday":
                dayOfWeek = DayOfWeek.Tuesday;
                return true;
            case "wed":
            case "weds":
            case "wednesday":
                dayOfWeek = DayOfWeek.Wednesday;
                return true;
            case "thu":
            case "thur":
            case "thurs":
            case "thursday":
                dayOfWeek = DayOfWeek.Thursday;
                return true;
            case "fri":
            case "friday":
                dayOfWeek = DayOfWeek.Friday;
                return true;
            case "sat":
            case "saturday":
                dayOfWeek = DayOfWeek.Saturday;
                return true;
            case "sun":
            case "sunday":
                dayOfWeek = DayOfWeek.Sunday;
                return true;
            default:
                dayOfWeek = default;
                return false;
        }
    }

    /// <summary>
    /// Splits a pipe-delimited date string and returns only dates that are
    /// today or in the future, sorted chronologically, in their original display-string form.
    /// Unparseable entries are silently dropped.
    /// </summary>
    public static IReadOnlyList<string> GetFutureDates(string pipeDates, DateTime? asOf = null)
    {
        if (string.IsNullOrWhiteSpace(pipeDates))
            return Array.Empty<string>();

        var today = (asOf ?? DateTime.UtcNow).Date;

        return pipeDates
            .Split('|', StringSplitOptions.RemoveEmptyEntries)
            .Select(d => d.Trim())
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .Select(d => (Raw: d, IsFuture: TryResolveFutureDate(d, today, out var resolved), ResolvedDate: resolved))
            .Where(x => x.IsFuture)
            .OrderBy(x => x.ResolvedDate)
            .Select(x => x.Raw)
            .ToList();
    }

    /// <summary>
    /// Returns true if a pipe-delimited date string contains at least one
    /// date that is today or in the future.
    /// </summary>
    public static bool HasFutureDates(string pipeDates) =>
        GetFutureDates(pipeDates).Count > 0;

    /// <summary>
    /// Derives the next market start and end date/time from the UpcomingDates and Hours fields.
    /// Returns null if no future dates are found or parsing fails.
    /// </summary>
    public static (DateTime? StartDate, DateTime? EndDate) GetNextMarketOccurrence(string upcomingDates, string hours)
    {
        var allFuture = GetAllFutureMarketOccurrences(upcomingDates, hours);
        return allFuture.Count > 0 ? allFuture[0] : (null, null);
    }

    /// <summary>
    /// Derives ALL future market start and end date/times from the UpcomingDates and Hours fields.
    /// </summary>
    public static List<(DateTime? StartDate, DateTime? EndDate)> GetAllFutureMarketOccurrences(string upcomingDates, string hours)
    {
        var futureDates = GetFutureDates(upcomingDates);
        var results = new List<(DateTime? StartDate, DateTime? EndDate)>();

        if (futureDates.Count == 0)
            return results;

        // Try to parse hours like "8:00 AM – 1:00 PM"
        var parts = hours.Split(new[] { '-', '–', '—' }, StringSplitOptions.RemoveEmptyEntries)
                         .Select(p => p.Trim())
                         .ToList();

        bool hasValidHours = false;
        TimeSpan startTime = TimeSpan.Zero;
        TimeSpan endTime = TimeSpan.Zero;

        if (parts.Count >= 2)
        {
            var startTimeStr = System.Text.RegularExpressions.Regex.Replace(parts[0], "([0-9])([AP]M)", "$1 $2", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            var endTimeStr = System.Text.RegularExpressions.Regex.Replace(parts[1], "([0-9])([AP]M)", "$1 $2", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (DateTime.TryParse(startTimeStr, out var st) && DateTime.TryParse(endTimeStr, out var et))
            {
                hasValidHours = true;
                startTime = st.TimeOfDay;
                endTime = et.TimeOfDay;
            }
        }

        var today = DateTime.UtcNow.Date;

        foreach (var dateStr in futureDates)
        {
            if (TryResolveFutureDate(dateStr, today, out var nextDate))
            {
                if (hasValidHours)
                {
                    var start = nextDate.Date.Add(startTime);
                    var end = nextDate.Date.Add(endTime);
                    if (end < start) end = end.AddDays(1);
                    results.Add((start, end));
                }
                else
                {
                    results.Add((nextDate.Date.AddHours(9), nextDate.Date.AddHours(17)));
                }
            }
        }

        return results;
    }
}
