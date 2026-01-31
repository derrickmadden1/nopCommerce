using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Discounts;
using Nop.Core.Domain.Orders;
using Nop.Plugin.DiscountRules.MultiBuy.Models;
using Nop.Plugin.DiscountRules.MultiBuy.Services;
using Nop.Services.Catalog;
using Nop.Services.Configuration;
using Nop.Services.Discounts;
using Nop.Services.Orders;
using Nop.Web.Framework.Components;
using Nop.Web.Framework.Infrastructure;
using Nop.Web.Models.Catalog;
using Nop.Web.Models.Catalog;
using Nop.Web.Models.ShoppingCart;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nop.Plugin.DiscountRules.MultiBuy.Components
{
    [ViewComponent(Name = "MultiBuyWidget")]
    public class MultiBuyWidgetViewComponent : NopViewComponent
    {
        private readonly IDiscountService _discountService;
        private readonly ISettingService _settingService;
        private readonly IProductService _productService;
        private readonly IShoppingCartService _shoppingCartService;
        private readonly IStoreContext _storeContext;
        private readonly IWorkContext _workContext;
        private readonly MultiBuyDiscountService _multiBuyDiscountService;

        public MultiBuyWidgetViewComponent(
            IDiscountService discountService,
            ISettingService settingService,
            IProductService productService,
            IShoppingCartService shoppingCartService,
            IStoreContext storeContext,
            IWorkContext workContext,
            MultiBuyDiscountService multiBuyDiscountService)
        {
            _discountService = discountService;
            _settingService = settingService;
            _productService = productService;
            _shoppingCartService = shoppingCartService;
            _storeContext = storeContext;
            _workContext = workContext;
            _multiBuyDiscountService = multiBuyDiscountService;
        }

        public async Task<IViewComponentResult> InvokeAsync(string widgetZone, object additionalData)
        {
            var model = new MultiBuyWidgetModel();

            // 1. Product Page Banner
            if (widgetZone.Equals(PublicWidgetZones.ProductDetailsOverviewTop) && additionalData is ProductDetailsModel productModel)
            {
                var allDiscounts = await _discountService.GetAllDiscountsAsync();
                foreach (var discount in allDiscounts)
                {
                    var settings = await GetMultiBuySettingsAsync(discount.Id);
                    if (settings != null && settings.EligibleProductIds.Contains(productModel.Id))
                    {
                        model.Message = $"Multibuy Deal: {settings.BundleSize} items for {settings.BundlePrice.ToString("C")}!";
                        model.IsProductPage = true;
                        break;
                    }
                }
            }
            // 2. Category/Catalog Page Product Box Badge
            else if (widgetZone.Equals(PublicWidgetZones.ProductBoxAddinfoMiddle) && additionalData is ProductOverviewModel overviewModel)
            {
                var allDiscounts = await _discountService.GetAllDiscountsAsync();
                foreach (var discount in allDiscounts)
                {
                    var settings = await GetMultiBuySettingsAsync(discount.Id);
                    if (settings != null && settings.EligibleProductIds.Contains(overviewModel.Id))
                    {
                         // Short badge message
                        model.Message = $"MultiBuy: {settings.BundleSize} for {settings.BundlePrice.ToString("C")}";
                        model.IsCatalogPage = true; 
                        break;
                    }
                }
            }
            // 3. Cart Page Features (Shared Data Prep)
            else if (widgetZone.Equals(PublicWidgetZones.OrderSummaryContentDeals) ||
                     widgetZone.Equals(PublicWidgetZones.OrderSummaryContentAfter) ||
                     widgetZone.Equals(PublicWidgetZones.OrderSummaryContentBefore))
            {
                var store = await _storeContext.GetCurrentStoreAsync();
                var customer = await _workContext.GetCurrentCustomerAsync();
                var cart = await _shoppingCartService.GetShoppingCartAsync(customer, ShoppingCartType.ShoppingCart, store.Id);

                if (!cart.Any())
                    return Content("");

                var allDiscounts = await _discountService.GetAllDiscountsAsync();

                // Feature: Total Savings (OrderSummaryContentDeals)
                if (widgetZone.Equals(PublicWidgetZones.OrderSummaryContentDeals))
                {
                    decimal totalSavings = 0;
                    foreach (var discount in allDiscounts)
                    {
                        var settings = await GetMultiBuySettingsAsync(discount.Id);
                        if (settings != null)
                        {
                            totalSavings += await _multiBuyDiscountService.CalculateDiscountAsync(cart, settings);
                        }
                    }
                    if (totalSavings > 0)
                    {
                        model.TotalSavings = totalSavings.ToString("C");
                    }
                }
                
                // Feature: Item Indicators (OrderSummaryContentAfter)
                if (widgetZone.Equals(PublicWidgetZones.OrderSummaryContentAfter))
                {
                     foreach (var discount in allDiscounts)
                     {
                        var settings = await GetMultiBuySettingsAsync(discount.Id);
                        if (settings != null)
                        {
                             var eligibleItems = cart.Where(i => settings.EligibleProductIds.Contains(i.ProductId)).ToList();
                             var eligibleCount = eligibleItems.Sum(i => i.Quantity);
                             if (eligibleCount > 0)
                             {
                                 var remainder = eligibleCount % settings.BundleSize;
                                 var message = "";
                                 if (remainder > 0)
                                     message = $"Buy {settings.BundleSize - remainder} more for deal!";
                                 else
                                     message = "MultiBuy Applied!";

                                 foreach (var item in eligibleItems)
                                 {
                                     if (!model.ItemMessages.ContainsKey(item.Id))
                                         model.ItemMessages.Add(item.Id, message);
                                 }
                             }
                        }
                     }
                }

                // Feature: Cart Nudge (OrderSummaryContentBefore) - Keep existing behavior but simplified
                if (widgetZone.Equals(PublicWidgetZones.OrderSummaryContentBefore))
                {
                    foreach (var discount in allDiscounts)
                    {
                        var settings = await GetMultiBuySettingsAsync(discount.Id);
                        if (settings != null)
                        {
                            var eligibleItemsCount = cart.Where(i => settings.EligibleProductIds.Contains(i.ProductId)).Sum(i => i.Quantity);
                            if (eligibleItemsCount > 0)
                            {
                                var remainder = eligibleItemsCount % settings.BundleSize;
                                if (remainder > 0)
                                {
                                    var needed = settings.BundleSize - remainder;
                                    model.Message = $"Add {needed} more eligible item(s) to complete your MultiBuy bundle!";
                                    model.IsCartPage = true;
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(model.Message) && string.IsNullOrEmpty(model.TotalSavings) && model.ItemMessages.Count == 0)
                return Content("");

            return View("~/Plugins/DiscountRules.MultiBuy/Views/MultiBuyWidget/PublicInfo.cshtml", model);
        }

        private async Task<MultiBuyRequirementSettings> GetMultiBuySettingsAsync(int discountId)
        {
            var requirements = await _discountService.GetAllDiscountRequirementsAsync(discountId);
            var mbReq = requirements.FirstOrDefault(r => r.DiscountRequirementRuleSystemName == DiscountRequirementDefaults.SYSTEM_NAME);
            
            if (mbReq != null)
            {
                var settingsKey = string.Format(DiscountRequirementDefaults.SETTINGS_KEY, mbReq.Id);
                var json = await _settingService.GetSettingByKeyAsync<string>(settingsKey);
                if (!string.IsNullOrEmpty(json))
                {
                    try 
                    { 
                        return Newtonsoft.Json.JsonConvert.DeserializeObject<MultiBuyRequirementSettings>(json);
                    }
                    catch { }
                }
            }
            return null;
        }
    }
}
