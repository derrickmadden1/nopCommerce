using Nop.Plugin.Widgets.SeoEnhancements.Domain;

namespace Nop.Plugin.Widgets.SeoEnhancements.Services;

public interface IFaqService
{
    Task<IList<SeoFaqItem>> GetFaqItemsAsync(SeoFaqEntityType entityType, int entityId, bool publishedOnly = true);
    Task<IList<SeoFaqItem>> GetAllFaqItemsAsync(bool publishedOnly = false);
    Task<SeoFaqItem?> GetFaqItemByIdAsync(int id);
    Task InsertFaqItemAsync(SeoFaqItem item);
    Task UpdateFaqItemAsync(SeoFaqItem item);
    Task DeleteFaqItemAsync(SeoFaqItem item);
}
