using Nop.Data;
using Nop.Plugin.Widgets.SeoEnhancements.Domain;

namespace Nop.Plugin.Widgets.SeoEnhancements.Services;

public class FaqService : IFaqService
{
    private readonly IRepository<SeoFaqItem> _faqItemRepository;

    public FaqService(IRepository<SeoFaqItem> faqItemRepository)
    {
        _faqItemRepository = faqItemRepository;
    }

    public async Task<IList<SeoFaqItem>> GetFaqItemsAsync(SeoFaqEntityType entityType, int entityId, bool publishedOnly = true)
    {
        var query = _faqItemRepository.Table
            .Where(f => f.EntityTypeId == (int)entityType && f.EntityId == entityId);

        if (publishedOnly)
            query = query.Where(f => f.Published);

        return await Task.FromResult(
            query.OrderBy(f => f.DisplayOrder).ThenBy(f => f.Id).ToList()
        );
    }

    public async Task<SeoFaqItem?> GetFaqItemByIdAsync(int id)
    {
        return await _faqItemRepository.GetByIdAsync(id);
    }

    public async Task InsertFaqItemAsync(SeoFaqItem item)
    {
        item.CreatedOnUtc = DateTime.UtcNow;
        item.UpdatedOnUtc = DateTime.UtcNow;
        await _faqItemRepository.InsertAsync(item);
    }

    public async Task UpdateFaqItemAsync(SeoFaqItem item)
    {
        item.UpdatedOnUtc = DateTime.UtcNow;
        await _faqItemRepository.UpdateAsync(item);
    }

    public async Task DeleteFaqItemAsync(SeoFaqItem item)
    {
        await _faqItemRepository.DeleteAsync(item);
    }
}
