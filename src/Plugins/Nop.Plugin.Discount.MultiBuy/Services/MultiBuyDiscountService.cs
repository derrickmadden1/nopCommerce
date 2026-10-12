using Nop.Core.Domain.Orders;
using Nop.Services.Orders;

namespace Nop.Plugin.DiscountRules.MultiBuy.Services
{
    /// <summary>
    /// Calculates the monetary value of a multi-buy discount for a cart,
    /// based on per-requirement settings.
    /// </summary>
    public class MultiBuyDiscountService
    {
        private readonly IShoppingCartService _shoppingCartService;

        public MultiBuyDiscountService(IShoppingCartService shoppingCartService)
        {
            _shoppingCartService = shoppingCartService;
        }

        public async Task<decimal> CalculateDiscountAsync(IList<ShoppingCartItem> cart, MultiBuyRequirementSettings settings)
        {
            if (cart == null || !cart.Any())
                return 0m;

            if (settings == null || settings.BundlePrice <= 0 || settings.EligibleProductIds == null || settings.EligibleProductIds.Count == 0)
                return 0m;

            if (settings.BundleType == MultiBuyBundleType.All)
            {
                // For "All" bundles: every eligible product must be present in the cart.
                // The number of complete bundles is the minimum quantity among all required products.
                var bundles = settings.EligibleProductIds
                    .Select(pid => cart.Where(i => i.ProductId == pid).Sum(i => i.Quantity))
                    .Min();

                if (bundles <= 0)
                    return 0m;

                decimal normalPriceOfBundledUnits = 0m;
                foreach (var pid in settings.EligibleProductIds)
                {
                    var itemsForProduct = cart.Where(i => i.ProductId == pid).ToList();
                    var unitsNeeded = bundles;
                    foreach (var item in itemsForProduct)
                    {
                        if (unitsNeeded <= 0) break;
                        var priceResult = await _shoppingCartService.GetUnitPriceAsync(item, includeDiscounts: false);
                        var takeUnits = Math.Min(item.Quantity, unitsNeeded);
                        normalPriceOfBundledUnits += priceResult.unitPrice * takeUnits;
                        unitsNeeded -= takeUnits;
                    }
                }

                var multiBuyPrice = bundles * settings.BundlePrice;
                var discount = normalPriceOfBundledUnits - multiBuyPrice;
                return discount > 0 ? discount : 0m;
            }
            else
            {
                if (settings.BundleSize < 1)
                    return 0m;

                var eligibleItems = cart
                    .Where(i => settings.EligibleProductIds.Contains(i.ProductId))
                    .ToList();

                if (!eligibleItems.Any())
                    return 0m;

                var eligibleQty = eligibleItems.Sum(i => i.Quantity);
                var bundles = eligibleQty / settings.BundleSize;
                Console.WriteLine($"[MultiBuy DEBUG] Eligible items: {eligibleItems.Count}, Qty: {eligibleQty}, Bundles: {bundles}");
                if (bundles <= 0)
                    return 0m;

                // Expand to a list of per-unit prices for all eligible items
                var unitPrices = new List<decimal>();
                foreach (var item in eligibleItems)
                {
                    var priceResult = await _shoppingCartService.GetUnitPriceAsync(item, includeDiscounts: false);
                    var unitPrice = priceResult.unitPrice;
                    for (var q = 0; q < item.Quantity; q++)
                        unitPrices.Add(unitPrice);
                }

                // Sort ascending and take the cheapest units to include in bundles
                unitPrices.Sort();
                var unitsInBundles = bundles * settings.BundleSize;
                var normalPriceOfBundledUnits = unitPrices.Take(unitsInBundles).Sum();

                var multiBuyPrice = bundles * settings.BundlePrice;
                var discount = normalPriceOfBundledUnits - multiBuyPrice;
                
                return discount > 0 ? discount : 0m;
            }
        }
    }
}
