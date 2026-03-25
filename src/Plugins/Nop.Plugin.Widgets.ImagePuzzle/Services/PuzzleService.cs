using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nop.Core.Caching;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Discounts;
using Nop.Services.Configuration;
using Nop.Services.Discounts;

namespace Nop.Plugin.Widgets.ImagePuzzle.Services;

public interface IPuzzleService
{
    Task<bool> IsProductInMultiBuyAsync(Product product);
}

public class PuzzleService : IPuzzleService
{
    private readonly IDiscountService _discountService;
    private readonly ISettingService _settingService;
    private readonly IStaticCacheManager _staticCacheManager;

    public PuzzleService(IDiscountService discountService,
        ISettingService settingService,
        IStaticCacheManager staticCacheManager)
    {
        _discountService = discountService;
        _settingService = settingService;
        _staticCacheManager = staticCacheManager;
    }

    public async Task<bool> IsProductInMultiBuyAsync(Product product)
    {
        var activeMultiBuyReqIds = await GetActiveMultiBuyRequirementIdsAsync();
        
        var result = await _staticCacheManager.GetAsync(
            _staticCacheManager.PrepareKeyForDefaultCache(new CacheKey($"Nop.multibuy-check-{product.Id}"), product),
            async () =>
            {
                var allSettings = await _settingService.GetAllSettingsAsync();
                foreach (var s in allSettings)
                {
                    if (string.IsNullOrEmpty(s.Name) || string.IsNullOrEmpty(s.Value)) continue;

                    bool isRequirementKey = s.Name.StartsWith("DiscountRequirement.RestrictedProductIds-", StringComparison.OrdinalIgnoreCase);
                    
                    if (isRequirementKey)
                    {
                        var parts = s.Name.Split('-');
                        if (parts.Length > 1 && int.TryParse(parts[1], out var reqId))
                        {
                            if (!activeMultiBuyReqIds.Contains(reqId))
                                continue;
                            
                            if (s.Value.Contains(product.Id.ToString()))
                                return true;
                        }
                    }
                }
                return false;
            });

        return result;
    }

    private async Task<HashSet<int>> GetActiveMultiBuyRequirementIdsAsync()
    {
        var cacheKey = _staticCacheManager.PrepareKeyForDefaultCache(new CacheKey("Nop.active-multibuy-req-ids-V1"));
        return await _staticCacheManager.GetAsync(cacheKey, async () =>
        {
            var discounts = await _discountService.GetAllDiscountsAsync();
            var ids = new HashSet<int>();
            foreach (var d in discounts)
            {
                if (!d.IsActive) continue;
                var reqs = await _discountService.GetAllDiscountRequirementsAsync(d.Id);
                foreach (var r in reqs)
                {
                    if (r.DiscountRequirementRuleSystemName == "DiscountRequirement.MultiBuy")
                        ids.Add(r.Id);
                }
            }
            return ids;
        });
    }
}
