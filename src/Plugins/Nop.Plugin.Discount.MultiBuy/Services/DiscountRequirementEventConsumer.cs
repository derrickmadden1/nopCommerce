using Newtonsoft.Json;
using Nop.Core.Domain.Discounts;
using Nop.Core.Events;
using Nop.Plugin.DiscountRules.MultiBuy;
using Nop.Plugin.DiscountRules.MultiBuy.Services;
using Nop.Services.Catalog;
using Nop.Services.Configuration;
using Nop.Services.Events;

namespace Nop.Plugin.Discount.MultiBuy.Services;

/// <summary>
/// Discount requirement event consumer for handling cleanup and lifecycle events.
/// </summary>
public class DiscountRequirementEventConsumer : IConsumer<EntityDeletedEvent<DiscountRequirement>>
{
    #region Fields

    private readonly IProductService _productService;
    private readonly ISettingService _settingService;

    #endregion

    #region Ctor

    public DiscountRequirementEventConsumer(
        IProductService productService,
        ISettingService settingService)
    {
        _productService = productService;
        _settingService = settingService;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Handle discount requirement deleted event.
    /// When a MultiBuy requirement is deleted, keep the linked bundle product in catalog for order integrity, but unpublish it.
    /// </summary>
    public async Task HandleEventAsync(EntityDeletedEvent<DiscountRequirement> eventMessage)
    {
        var discountRequirement = eventMessage?.Entity;
        if (discountRequirement == null)
            return;

        if (!discountRequirement.DiscountRequirementRuleSystemName.Equals(DiscountRequirementDefaults.SYSTEM_NAME, StringComparison.OrdinalIgnoreCase))
            return;

        var settingsKey = string.Format(DiscountRequirementDefaults.SETTINGS_KEY, discountRequirement.Id);
        var requirementSettingsJson = await _settingService.GetSettingByKeyAsync<string>(settingsKey);

        if (!string.IsNullOrEmpty(requirementSettingsJson))
        {
            try
            {
                var settings = JsonConvert.DeserializeObject<MultiBuyRequirementSettings>(requirementSettingsJson);
                if (settings?.BundledProductId > 0)
                {
                    var product = await _productService.GetProductByIdAsync(settings.BundledProductId);
                    if (product != null)
                    {
                        product.Published = false;
                        product.UpdatedOnUtc = DateTime.UtcNow;
                        await _productService.UpdateProductAsync(product);
                    }
                }
            }
            catch
            {
                // Ignore deserialization exceptions during deletion
            }
        }

        var setting = await _settingService.GetSettingAsync(settingsKey);
        if (setting != null)
            await _settingService.DeleteSettingAsync(setting);
    }

    #endregion
}
