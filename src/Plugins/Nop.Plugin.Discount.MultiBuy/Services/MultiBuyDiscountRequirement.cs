using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Http;
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
using Nop.Services.Helpers;

namespace Nop.Plugin.DiscountRules.MultiBuy.Services
{
    public class MultiBuyDiscountRequirement : BasePlugin, IDiscountRequirementRule, IWidgetPlugin
    {
        private readonly IShoppingCartService _shoppingCartService;
        private readonly ISettingService _settingService;
        private readonly LinkGenerator _linkGenerator;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IWebHelper _webHelper;
        private readonly ILocalizationService _localizationService;

        public MultiBuyDiscountRequirement(IShoppingCartService shoppingCartService,
                                           ISettingService settingService,
                                           LinkGenerator linkGenerator,
                                           IHttpContextAccessor httpContextAccessor,
                                           IWebHelper webHelper,
                                           ILocalizationService localizationService)
        {
            _shoppingCartService = shoppingCartService;
            _settingService = settingService;
            _linkGenerator = linkGenerator;
            _httpContextAccessor = httpContextAccessor;
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

            var storeId = request.Store?.Id ?? 0;
            var cart = await _shoppingCartService.GetShoppingCartAsync(request.Customer, ShoppingCartType.ShoppingCart, storeId: storeId);
            
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

            result.IsValid = eligibleCount >= requirementSettings.BundleSize;
            
            return result;
        }

        public string GetConfigurationUrl(int discountId, int? discountRequirementId)
        {
            return _linkGenerator.GetUriByAction(
                _httpContextAccessor.HttpContext,
                "Configure", "DiscountRulesMultiBuy",
                new { discountId, discountRequirementId }, _webHelper.GetCurrentRequestProtocol());
        }

        public Task<IList<string>> GetWidgetZonesAsync()
        {
            return Task.FromResult<IList<string>>(new List<string>
            {
                Nop.Web.Framework.Infrastructure.PublicWidgetZones.ProductDetailsOverviewTop,
                Nop.Web.Framework.Infrastructure.PublicWidgetZones.OrderSummaryContentBefore,
                Nop.Web.Framework.Infrastructure.PublicWidgetZones.OrderSummaryContentDeals,
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