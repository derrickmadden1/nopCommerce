using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Nop.Core;
using Nop.Core.Domain.Discounts;
using Nop.Core.Domain.Orders;
using Nop.Services.Configuration;
using Nop.Services.Discounts;
using Nop.Services.Orders;
using Nop.Services.Plugins;

using Nop.Services.Cms;
using Nop.Services.Localization;
using System.Collections.Generic;

namespace Nop.Plugin.DiscountRules.MultiBuy.Services
{
    public class MultiBuyDiscountRequirement : BasePlugin, IDiscountRequirementRule, IWidgetPlugin
    {
        private readonly IShoppingCartService _shoppingCartService;
        private readonly ISettingService _settingService;
        private readonly IActionContextAccessor _actionContextAccessor;
        private readonly IUrlHelperFactory _urlHelperFactory;
        private readonly IWebHelper _webHelper;
        private readonly ILocalizationService _localizationService;

        public MultiBuyDiscountRequirement(IShoppingCartService shoppingCartService,
                                           ISettingService settingService,
                                           IActionContextAccessor actionContextAccessor,
                                           IUrlHelperFactory urlHelperFactory,
                                           IWebHelper webHelper,
                                           ILocalizationService localizationService)
        {
            _shoppingCartService = shoppingCartService;
            _settingService = settingService;
            _actionContextAccessor = actionContextAccessor;
            _urlHelperFactory = urlHelperFactory;
            _webHelper = webHelper;
            _localizationService = localizationService;
        }

        public override async Task InstallAsync()
        {
            //locales
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.DiscountRules.MultiBuy.Fields.Products", "Restricted products");
            // Also adding a fallback hint if needed, though not requested
            // await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.DiscountRules.MultiBuy.Fields.Products.Hint", "Select the products eligible for this MultiBuy discount.");

            await base.InstallAsync();
        }

        public override async Task UninstallAsync()
        {
            //locales
            await _localizationService.DeleteLocaleResourceAsync("Plugins.DiscountRules.MultiBuy.Fields.Products");
            
            await base.UninstallAsync();
        }

        public async Task<DiscountRequirementValidationResult> CheckRequirementAsync(DiscountRequirementValidationRequest request)
        {
            var result = new DiscountRequirementValidationResult();

            if (request.Customer is null || request.DiscountRequirementId == 0)
                return result;

            var cart = await _shoppingCartService.GetShoppingCartAsync(request.Customer, ShoppingCartType.ShoppingCart);
            if (cart == null || cart.Count == 0)
                return result;

            var settingsKey = string.Format(DiscountRequirementDefaults.SETTINGS_KEY, request.DiscountRequirementId);
            var requirementSettingsJson = await _settingService.GetSettingByKeyAsync<string>(settingsKey);
            
            if (string.IsNullOrEmpty(requirementSettingsJson))
                return result;

            MultiBuyRequirementSettings requirementSettings;
            try 
            {
                requirementSettings = Newtonsoft.Json.JsonConvert.DeserializeObject<MultiBuyRequirementSettings>(requirementSettingsJson);
            }
            catch 
            {
                // if deserialization fails (e.g. old data format), return invalid
                return result;
            }

            if (requirementSettings == null || requirementSettings.BundleSize < 1 || requirementSettings.EligibleProductIds == null || requirementSettings.EligibleProductIds.Count == 0)
                return result;

            var eligibleCount = 0;
            foreach (var item in cart)
            {
                if (requirementSettings.EligibleProductIds.Contains(item.ProductId))
                    eligibleCount += item.Quantity;
            }

            // valid if we have at least one full bundle worth of eligible items
            result.IsValid = eligibleCount >= requirementSettings.BundleSize;
            return result;
        }

        public string GetConfigurationUrl(int discountId, int? discountRequirementId)
        {
            var urlHelper = _urlHelperFactory.GetUrlHelper(_actionContextAccessor.ActionContext);

            return urlHelper.Action("Configure", "DiscountRulesMultiBuy",
                new { discountId, discountRequirementId }, _webHelper.GetCurrentRequestProtocol());
        }

        public Task<IList<string>> GetWidgetZonesAsync()
        {
            return Task.FromResult<IList<string>>(new List<string>
            {
                Nop.Web.Framework.Infrastructure.PublicWidgetZones.ProductDetailsOverviewTop,
                Nop.Web.Framework.Infrastructure.PublicWidgetZones.OrderSummaryContentBefore,
                Nop.Web.Framework.Infrastructure.PublicWidgetZones.OrderSummaryContentDeals,
                Nop.Web.Framework.Infrastructure.PublicWidgetZones.OrderSummaryContentAfter,
                Nop.Web.Framework.Infrastructure.PublicWidgetZones.ProductBoxAddinfoMiddle
            });
        }

        public Type GetWidgetViewComponent(string widgetZone)
        {
            return typeof(Components.MultiBuyWidgetViewComponent);
        }

        public bool HideInWidgetList => false;
    }
}