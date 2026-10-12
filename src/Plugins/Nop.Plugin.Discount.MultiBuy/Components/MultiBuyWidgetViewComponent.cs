using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Core.Http;
using Nop.Plugin.DiscountRules.MultiBuy.Models;
using Nop.Plugin.DiscountRules.MultiBuy.Services;
using Nop.Services.Catalog;
using Nop.Services.Configuration;
using Nop.Services.Discounts;
using Nop.Services.Localization;
using Nop.Services.Orders;
using Nop.Services.Seo;
using Nop.Web.Framework.Components;
using Nop.Web.Framework.Infrastructure;
using Nop.Web.Framework.Mvc.Routing;
using Nop.Web.Models.Catalog;

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
        private readonly IUrlRecordService _urlRecordService;
        private readonly MultiBuyDiscountService _multiBuyDiscountService;

        public MultiBuyWidgetViewComponent(
            IDiscountService discountService,
            ISettingService settingService,
            IProductService productService,
            IShoppingCartService shoppingCartService,
            IStoreContext storeContext,
            IWorkContext workContext,
            IUrlRecordService urlRecordService,
            MultiBuyDiscountService multiBuyDiscountService)
        {
            _discountService = discountService;
            _settingService = settingService;
            _productService = productService;
            _shoppingCartService = shoppingCartService;
            _storeContext = storeContext;
            _workContext = workContext;
            _urlRecordService = urlRecordService;
            _multiBuyDiscountService = multiBuyDiscountService;
        }

        public async Task<IViewComponentResult> InvokeAsync(string widgetZone, object additionalData)
        {
            var model = new MultiBuyWidgetModel();

            // 1. Product Page Banner
            if (widgetZone.Equals(PublicWidgetZones.ProductDetailsOverviewTop, StringComparison.OrdinalIgnoreCase) && additionalData is ProductDetailsModel productModel)
            {
                var allDiscounts = await _discountService.GetAllDiscountsAsync();
                foreach (var discount in allDiscounts)
                {
                    var settings = await GetMultiBuySettingsAsync(discount.Id);
                    if (settings != null && settings.EligibleProductIds.Contains(productModel.Id))
                    {
                        var isAll = settings.BundleType == MultiBuyBundleType.All;
                        var prefix = isAll ? "All" : "Any";
                        var count = isAll ? settings.EligibleProductIds.Count : settings.BundleSize;
                        model.Message = $"Multibuy Deal: {prefix} {count} items for {settings.BundlePrice.ToString("C")}!";
                        model.IsProductPage = true;
                        break;
                    }
                }
            }
            // 2. Category/Catalog Page Product Box Badge
            else if (widgetZone.Equals(PublicWidgetZones.ProductBoxAddinfoMiddle, StringComparison.OrdinalIgnoreCase) && additionalData is ProductOverviewModel overviewModel)
            {
                var allDiscounts = await _discountService.GetAllDiscountsAsync();
                foreach (var discount in allDiscounts)
                {
                    var settings = await GetMultiBuySettingsAsync(discount.Id);
                    if (settings != null && settings.EligibleProductIds.Contains(overviewModel.Id))
                    {
                        var isAll = settings.BundleType == MultiBuyBundleType.All;
                        var prefix = isAll ? "All" : "Any";
                        var count = isAll ? settings.EligibleProductIds.Count : settings.BundleSize;
                        model.Message = $"MultiBuy: {prefix} {count} for {settings.BundlePrice.ToString("C")}";
                        model.IsCatalogPage = true; 
                        break;
                    }
                }
            }
            // 3. Cart Page Features (Shared Data Prep)
            else if (widgetZone.Equals(PublicWidgetZones.OrderSummaryContentDeals, StringComparison.OrdinalIgnoreCase) ||
                     widgetZone.Equals(PublicWidgetZones.OrderSummaryContentAfter, StringComparison.OrdinalIgnoreCase) ||
                     widgetZone.Equals(PublicWidgetZones.OrderSummaryContentBefore, StringComparison.OrdinalIgnoreCase))
            {
                var store = await _storeContext.GetCurrentStoreAsync();
                var customer = await _workContext.GetCurrentCustomerAsync();
                var cart = await _shoppingCartService.GetShoppingCartAsync(customer, ShoppingCartType.ShoppingCart, store.Id);

                if (!cart.Any())
                    return Content("");

                var allDiscounts = await _discountService.GetAllDiscountsAsync();
                
                decimal totalSavings = 0;
                if (allDiscounts?.Any() == true)
                {
                    foreach (var discount in allDiscounts)
                    {
                        var settings = await GetMultiBuySettingsAsync(discount.Id);
                        if (settings != null)
                        {
                            var mbDiscountAmount = await _multiBuyDiscountService.CalculateDiscountAsync(cart, settings);
                            totalSavings += mbDiscountAmount;
                        }
                    }
                }

                if (totalSavings > 0 && widgetZone.Equals(PublicWidgetZones.OrderSummaryContentDeals, StringComparison.OrdinalIgnoreCase))
                {
                    model.TotalSavings = totalSavings.ToString("C");
                }
                
                // Feature: Item Indicators
                if (widgetZone.Equals(PublicWidgetZones.OrderSummaryContentBefore, StringComparison.OrdinalIgnoreCase) ||
                    widgetZone.Equals(PublicWidgetZones.OrderSummaryContentAfter, StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var discount in allDiscounts)
                    {
                        var settings = await GetMultiBuySettingsAsync(discount.Id);
                        if (settings != null)
                        {
                            var eligibleItems = cart.Where(i => settings.EligibleProductIds.Contains(i.ProductId)).ToList();
                            if (eligibleItems.Any())
                            {
                                if (settings.BundleType == MultiBuyBundleType.All)
                                {
                                    var cartProductIds = cart.Where(i => i.Quantity > 0).Select(i => i.ProductId).Distinct().ToHashSet();
                                    var missingProductIds = settings.EligibleProductIds.Where(id => !cartProductIds.Contains(id)).ToList();

                                    if (missingProductIds.Any())
                                    {
                                        var missingProducts = await _productService.GetProductsByIdsAsync(missingProductIds.ToArray());
                                        var missingModels = new List<MissingBundleProductModel>();
                                        foreach (var mp in missingProducts)
                                        {
                                            var seName = await _urlRecordService.GetSeNameAsync(mp);
                                            var prodUrl = Url.RouteUrl(NopRoutingDefaults.RouteName.Generic.Product, new { SeName = seName });
                                            var addUrl = Url.RouteUrl(NopRouteNames.Ajax.ADD_PRODUCT_TO_CART_CATALOG, new { productId = mp.Id, shoppingCartTypeId = (int)ShoppingCartType.ShoppingCart, quantity = 1 });

                                            missingModels.Add(new MissingBundleProductModel
                                            {
                                                ProductId = mp.Id,
                                                Name = mp.Name,
                                                ProductUrl = prodUrl,
                                                AddToCartUrl = addUrl
                                            });
                                        }

                                        var promptMsg = $"Complete the bundle for {settings.BundlePrice.ToString("C")}!";
                                        foreach (var item in eligibleItems)
                                        {
                                            if (!model.ItemPrompts.ContainsKey(item.Id))
                                            {
                                                model.ItemPrompts.Add(item.Id, new MultiBuyCartItemPromptModel
                                                {
                                                    Message = promptMsg,
                                                    IsAllBundle = true,
                                                    MissingProducts = missingModels
                                                });
                                                model.ItemMessages[item.Id] = promptMsg;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        var promptMsg = "MultiBuy Applied!";
                                        foreach (var item in eligibleItems)
                                        {
                                            if (!model.ItemPrompts.ContainsKey(item.Id))
                                            {
                                                model.ItemPrompts.Add(item.Id, new MultiBuyCartItemPromptModel
                                                {
                                                    Message = promptMsg,
                                                    IsAllBundle = true,
                                                    MissingProducts = new List<MissingBundleProductModel>()
                                                });
                                                model.ItemMessages[item.Id] = promptMsg;
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    var eligibleCount = eligibleItems.Sum(i => i.Quantity);
                                    if (eligibleCount > 0)
                                    {
                                        var remainder = eligibleCount % settings.BundleSize;
                                        var message = remainder > 0
                                            ? $"Buy {settings.BundleSize - remainder} more for deal!"
                                            : "MultiBuy Applied!";

                                        foreach (var item in eligibleItems)
                                        {
                                            if (!model.ItemPrompts.ContainsKey(item.Id))
                                            {
                                                model.ItemPrompts.Add(item.Id, new MultiBuyCartItemPromptModel
                                                {
                                                    Message = message,
                                                    IsAllBundle = false
                                                });
                                                model.ItemMessages[item.Id] = message;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                // Feature: Cart Nudge (OrderSummaryContentBefore)
                if (widgetZone.Equals(PublicWidgetZones.OrderSummaryContentBefore, StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var discount in allDiscounts)
                    {
                        var settings = await GetMultiBuySettingsAsync(discount.Id);
                        if (settings != null)
                        {
                            var eligibleItems = cart.Where(i => settings.EligibleProductIds.Contains(i.ProductId)).ToList();
                            if (eligibleItems.Any())
                            {
                                if (settings.BundleType == MultiBuyBundleType.All)
                                {
                                    var cartProductIds = cart.Where(i => i.Quantity > 0).Select(i => i.ProductId).Distinct().ToHashSet();
                                    var missingProductIds = settings.EligibleProductIds.Where(id => !cartProductIds.Contains(id)).ToList();
                                    if (missingProductIds.Any())
                                    {
                                        model.Message = $"Complete your MultiBuy bundle for {settings.BundlePrice.ToString("C")}! Add the missing item(s) below to qualify.";
                                        model.IsCartPage = true;
                                        break;
                                    }
                                }
                                else
                                {
                                    var eligibleItemsCount = eligibleItems.Sum(i => i.Quantity);
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
            }

            if (string.IsNullOrEmpty(model.Message) && string.IsNullOrEmpty(model.TotalSavings) && model.ItemMessages.Count == 0 && model.ItemPrompts.Count == 0)
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
