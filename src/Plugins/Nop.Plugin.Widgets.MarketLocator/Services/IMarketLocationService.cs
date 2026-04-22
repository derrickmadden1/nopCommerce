using Nop.Core;
using Nop.Plugin.Widgets.MarketLocator.Domain;

namespace Nop.Plugin.Widgets.MarketLocator.Services;

public interface IMarketLocationService
{
    Task<IPagedList<MarketLocation>> GetAllAsync(
        bool showUnpublished = false,
        string? frequency = null,
        int pageIndex = 0,
        int pageSize = int.MaxValue);

    Task<MarketLocation?> GetByIdAsync(int id);
    Task InsertAsync(MarketLocation location);
    Task UpdateAsync(MarketLocation location);
    Task DeleteAsync(MarketLocation location);

    /// <summary>
    /// Returns published locations as lightweight DTOs for the public map JSON endpoint.
    /// Status (today / soon / upcoming) is calculated relative to UTC now.
    /// </summary>
    Task<IList<MarketLocationDto>> GetPublishedDtosAsync();
}
