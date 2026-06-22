using Nop.Core.Domain.Catalog;
using Nop.Plugin.Widgets.SeoEnhancements.Domain;

namespace Nop.Plugin.Widgets.SeoEnhancements.Services;

public interface IFaqGenerationService
{
    /// <summary>
    /// Calls Azure OpenAI to generate candidate FAQ pairs for a product.
    /// Returns an empty list (never throws to the caller) on failure — check logs.
    /// </summary>
    Task<IList<GeneratedFaqPair>> GenerateForProductAsync(Product product, int pairCount);

    Task<IList<GeneratedFaqPair>> GenerateForCategoryAsync(Category category, int pairCount);
}
