using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Nop.Core;
using Nop.Core.Caching;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Discounts;
using Nop.Core.Domain.Stores;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Discounts;
using Nop.Core.Infrastructure;

namespace Nop.Plugin.Widgets.ImagePuzzle.Services;

/// <summary>
/// Custom price calculation service to handle puzzle solving discounts per product with precedence logic
/// </summary>
public partial class PuzzlePriceCalculationService : PriceCalculationService
{
    private readonly IGenericAttributeService _genericAttributeService;
    private readonly ISettingService _settingService;
    private readonly IPuzzleService _puzzleService;

    public PuzzlePriceCalculationService(CatalogSettings catalogSettings,
        CurrencySettings currencySettings,
        ICategoryService categoryService,
        ICurrencyService currencyService,
        ICustomerService customerService,
        IDiscountService discountService,
        IManufacturerService manufacturerService,
        IProductAttributeParser productAttributeParser,
        IProductService productService,
        IStaticCacheManager staticCacheManager,
        IGenericAttributeService genericAttributeService,
        ISettingService settingService,
        IPuzzleService puzzleService)
        : base(catalogSettings, currencySettings, categoryService, currencyService, customerService, discountService, manufacturerService, productAttributeParser, productService, staticCacheManager)
    {
        _genericAttributeService = genericAttributeService;
        _settingService = settingService;
        _puzzleService = puzzleService;
    }

    public override async Task<(decimal priceWithoutDiscounts, decimal finalPrice, decimal appliedDiscountAmount, List<Discount> appliedDiscounts)> GetFinalPriceAsync(Product product,
        Customer customer,
        Store store,
        decimal? overriddenProductPrice,
        decimal additionalCharge,
        bool includeDiscounts,
        int quantity,
        DateTime? rentalStartDate,
        DateTime? rentalEndDate)
    {
        var cacheKey = _staticCacheManager.PrepareKeyForDefaultCache(new CacheKey($"Nop.puzzlepriceV29-{product.Id}-{overriddenProductPrice}-{additionalCharge}-{includeDiscounts}-{quantity}-{string.Join(",", await _customerService.GetCustomerRoleIdsAsync(customer))}-{store.Id}-{customer.Id}"),
            product,
            overriddenProductPrice,
            additionalCharge,
            includeDiscounts,
            quantity,
            await _customerService.GetCustomerRoleIdsAsync(customer),
            store);

        if (product.IsRental)
            cacheKey.CacheTime = 0;

        var result = await _staticCacheManager.GetAsync(cacheKey, async () => 
        {
            return await base.GetFinalPriceAsync(product, customer, store, overriddenProductPrice, additionalCharge, includeDiscounts, quantity, rentalStartDate, rentalEndDate);
        });

        // Debug logging for price results
        /*try {
            var logPath = "C:\\Users\\madde\\source\\repos\\derrickmadden1\\nopCommerce\\src\\Presentation\\Nop.Web\\logs\\puzzle_price_debug.log";
            System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] RESULT GetFinalPriceAsync - Product: {product.Id}, Original: {result.priceWithoutDiscounts}, Final: {result.finalPrice}, Applied: {result.appliedDiscountAmount}, Discounts: {result.appliedDiscounts.Count}\n");
        } catch {}*/

        return result;
    }

    protected override async Task<IList<Discount>> GetAllowedDiscountsAsync(Product product, Customer customer)
    {
        /*var logPath = "C:\\Users\\madde\\source\\repos\\derrickmadden1\\nopCommerce\\src\\Presentation\\Nop.Web\\logs\\puzzle_price_debug.log";
        string logEntry = $"[{DateTime.Now}] START GetAllowedDiscountsAsync - Product: {product.Id}, Customer: {customer.Id}\n";*/
        try
        {
            var allowedDiscounts = await base.GetAllowedDiscountsAsync(product, customer);
            var puzzleDiscountIds = await GetPuzzleDiscountIdsAsync();
            
            if (!puzzleDiscountIds.Any())
            {
                //logEntry += " - EXIT: No puzzle discounts configured.\n";
                //System.IO.File.AppendAllText(logPath, logEntry);
                return allowedDiscounts;
            }

            // Determine if this product is part of an active MultiBuy saving
            bool isMultiBuy = await IsProductInMultiBuyAsync(product);
            //logEntry += $" - isMultiBuy: {isMultiBuy}\n";

            // Determine if the puzzle for THIS product is solved
            var solvedIdsString = await _genericAttributeService.GetAttributeAsync<string>(customer, "SolvedPuzzleProductIds") ?? "";
            var solvedIds = solvedIdsString.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            bool isSolved = solvedIds.Contains(product.Id.ToString());
            //logEntry += $" - isSolved: {isSolved} (SolvedIds: {solvedIdsString})\n";

            var finalDiscounts = new List<Discount>();

            // Case A: Product is in a MultiBuy saving -> NO puzzle discounts allowed
            if (isMultiBuy)
            {
                //logEntry += " - SKIPPING: MultiBuy override active.\n";
                foreach (var d in allowedDiscounts)
                {
                    if (!puzzleDiscountIds.Contains(d.Id))
                        finalDiscounts.Add(d);
                }
                //System.IO.File.AppendAllText(logPath, logEntry);
                return finalDiscounts;
            }

            // Case B: Not in MultiBuy -> Puzzle discount allowed IF solved
            foreach (var d in allowedDiscounts)
            {
                if (!puzzleDiscountIds.Contains(d.Id))
                {
                    finalDiscounts.Add(d);
                }
                else
                {
                    //logEntry += $" - Note: Removing pre-existing puzzle discount {d.Id} to re-validate.\n";
                }
            }

            if (isSolved)
            {
                var couponCodes = await _customerService.ParseAppliedDiscountCouponCodesAsync(customer);
                foreach (var discountId in puzzleDiscountIds)
                {
                    var discount = await _discountService.GetDiscountByIdAsync(discountId);
                    //if (discount == null) { logEntry += $" - Error: Discount {discountId} not found.\n"; continue; }
                    //if (!discount.IsActive) { logEntry += $" - Note: Discount {discountId} is inactive.\n"; continue; }

                    var valResult = await _discountService.ValidateDiscountAsync(discount, customer, couponCodes);
                    //logEntry += $" - Validating Discount {discountId}: IsValid={valResult.IsValid}, Errors=[{string.Join(", ", valResult.Errors ?? new List<string>())}]\n";
                    
                    if (valResult.IsValid)
                        finalDiscounts.Add(discount);
                }
            }
            else
            {
                //logEntry += " - Result: Product not solved, no extra discounts added.\n";
            }

            //logEntry += $" - FINAL DISCOUNT COUNT: {finalDiscounts.Count}\n";
            //System.IO.File.AppendAllText(logPath, logEntry);
            return finalDiscounts;
        }
        catch (Exception ex)
        {
            //logEntry += $" [ERROR] {ex.Message}\n";
            //System.IO.File.AppendAllText(logPath, logEntry);
            // Fallback to base in case of errors
            return await base.GetAllowedDiscountsAsync(product, customer);
        }
    }

    private async Task<List<int>> GetPuzzleDiscountIdsAsync()
    {
        return await _staticCacheManager.GetAsync(
            _staticCacheManager.PrepareKeyForDefaultCache(new CacheKey("Nop.Plugin.Widgets.ImagePuzzle.AllPuzzleDiscountIds")),
            async () =>
            {
                var allDiscounts = await _discountService.GetAllDiscountsAsync(isActive: true);
                var result = new List<int>();
                foreach (var d in allDiscounts)
                {
                    var reqs = await _discountService.GetAllDiscountRequirementsAsync(d.Id);
                    if (reqs.Any(r => r.DiscountRequirementRuleSystemName == "Widgets.ImagePuzzle"))
                        result.Add(d.Id);
                }
                return result;
            });
    }

    /// <summary>
    /// Check if the product is eligible for any MultiBuy discount
    /// </summary>
    private async Task<bool> IsProductInMultiBuyAsync(Product product)
    {
        return await _puzzleService.IsProductInMultiBuyAsync(product);
    }
}
