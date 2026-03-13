using Nop.Core;
using Nop.Data;
using Nop.Plugin.Widgets.MarketLocator.Domain;

namespace Nop.Plugin.Widgets.MarketLocator.Services;

public class MarketLocationService : IMarketLocationService
{
    private readonly IRepository<MarketLocation> _repository;

    public MarketLocationService(IRepository<MarketLocation> repository)
    {
        _repository = repository;
    }

    public async Task<IPagedList<MarketLocation>> GetAllAsync(
        bool showUnpublished = false,
        string? frequency = null,
        int pageIndex = 0,
        int pageSize = int.MaxValue)
    {
        return await _repository.GetAllPagedAsync(query =>
        {
            if (!showUnpublished)
                query = query.Where(m => m.Published);

            if (!string.IsNullOrWhiteSpace(frequency))
                query = query.Where(m => m.Frequency == frequency);

            return query.OrderBy(m => m.DisplayOrder).ThenBy(m => m.Name);
        }, pageIndex, pageSize);
    }

    public async Task<MarketLocation?> GetByIdAsync(int id) =>
        await _repository.GetByIdAsync(id);

    public async Task InsertAsync(MarketLocation location)
    {
        ArgumentNullException.ThrowIfNull(location);
        await _repository.InsertAsync(location);
    }

    public async Task UpdateAsync(MarketLocation location)
    {
        ArgumentNullException.ThrowIfNull(location);
        await _repository.UpdateAsync(location);
    }

    public async Task DeleteAsync(MarketLocation location)
    {
        ArgumentNullException.ThrowIfNull(location);
        await _repository.DeleteAsync(location);
    }

    public async Task<IList<MarketLocationDto>> GetPublishedDtosAsync()
    {
        var locations = await _repository.GetAllAsync(query =>
            query.Where(m => m.Published)
                 .OrderBy(m => m.DisplayOrder)
                 .ThenBy(m => m.Name));

        var today = DateTime.UtcNow.Date;

        return locations
            .Select(m =>
            {
                // Only surface future dates — past ones are silently dropped.
                var dates = MarketDateHelper.GetFutureDates(m.UpcomingDates);
                if (!dates.Any()) return null; // hide markets with no remaining dates

                var status = ComputeStatus(dates, today);

                return new MarketLocationDto
                {
                    Id         = m.Id,
                    Name       = m.Name,
                    Address    = m.Address,
                    City       = m.City,
                    Latitude   = (double)m.Latitude,
                    Longitude  = (double)m.Longitude,
                    Hours      = m.Hours,
                    Dates      = dates.ToList(),
                    Frequency  = m.Frequency,
                    Status     = status,
                };
            })
            .Where(dto => dto is not null)
            .Select(dto => dto!)
            .ToList();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string ComputeStatus(IReadOnlyList<string> dates, DateTime today)
    {
        if (!dates.Any()) return "upcoming";

        if (MarketDateHelper.TryParseDate(dates[0], today.Year, out var firstDate))
        {
            if (firstDate.Date == today)               return "today";
            if (firstDate.Date <= today.AddDays(7))    return "soon";
        }

        return "upcoming";
    }
}
