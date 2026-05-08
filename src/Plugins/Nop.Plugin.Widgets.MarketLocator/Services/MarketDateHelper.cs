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
    /// Strips a leading day-of-week ("Sat, ") before parsing.
    /// Tries "Mar 14 2025" first, then bare "Mar 14" as fallback.
    /// </summary>
    public static bool TryParseDate(string raw, int assumedYear, out DateTime result)
    {
        var cleaned = System.Text.RegularExpressions.Regex.Replace(
            raw.Trim(), @"^\w+,\s*", "");

        return DateTime.TryParse($"{cleaned} {assumedYear}", out result)
            || DateTime.TryParse(cleaned, out result);
    }

    /// <summary>
    /// Splits a pipe-delimited date string and returns only dates that are
    /// today or in the future, in their original display-string form.
    /// Unparseable entries are silently dropped.
    /// </summary>
    public static IReadOnlyList<string> GetFutureDates(string pipeDates, DateTime? asOf = null)
    {
        var today = (asOf ?? DateTime.UtcNow).Date;
        var year  = today.Year;

        return pipeDates
            .Split('|', StringSplitOptions.RemoveEmptyEntries)
            .Select(d => d.Trim())
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .Where(d =>
            {
                // Keep the date if it parses to today or later.
                // If it won't parse at all, drop it — better than surfacing
                // a stale or malformed entry to the customer.
                if (!TryParseDate(d, year, out var dt)) return false;

                // Handle year-boundary: "Jan 5" in December should be read as
                // next year, not this January (which has already passed).
                if (dt.Date < today && year == today.Year)
                {
                    // Only roll over if the current month is late (Nov/Dec) and the parsed month is early (Jan/Feb/Mar)
                    if (today.Month >= 11 && dt.Month <= 3)
                    {
                        if (TryParseDate(d, year + 1, out var nextYearDt) && nextYearDt.Date >= today)
                            return true;
                    }

                    return false;
                }

                return dt.Date >= today;
            })
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
        int year = today.Year;

        foreach (var dateStr in futureDates)
        {
            if (TryParseDate(dateStr, year, out var nextDate))
            {
                // Handle year-boundary for parsed dates
                if (nextDate.Date < today && year == today.Year && today.Month >= 11 && nextDate.Month <= 3)
                {
                    TryParseDate(dateStr, year + 1, out nextDate);
                }

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
