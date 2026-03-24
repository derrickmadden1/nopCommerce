using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Caching;
using Nop.Core.Domain.Discounts;
using Nop.Services.Common;
using Nop.Services.Customers;
using Nop.Services.Discounts;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Widgets.ImagePuzzle.Controllers;

public partial class PuzzleController : BasePluginController
{
    private readonly ICustomerService _customerService;
    private readonly IDiscountService _discountService;
    private readonly IGenericAttributeService _genericAttributeService;
    private readonly IStaticCacheManager _staticCacheManager;
    private readonly IStoreContext _storeContext;
    private readonly IWorkContext _workContext;

    public PuzzleController(ICustomerService customerService,
        IDiscountService discountService,
        IGenericAttributeService genericAttributeService,
        IStaticCacheManager staticCacheManager,
        IStoreContext storeContext,
        IWorkContext workContext)
    {
        _customerService = customerService;
        _discountService = discountService;
        _genericAttributeService = genericAttributeService;
        _staticCacheManager = staticCacheManager;
        _storeContext = storeContext;
        _workContext = workContext;
    }

    [HttpPost]
    public async Task<IActionResult> ApplyPuzzleDiscount()
    {
        var customer = await _workContext.GetCurrentCustomerAsync();
        var store = await _storeContext.GetCurrentStoreAsync();

        // Mark as solved as well when applying discount
        await _genericAttributeService.SaveAttributeAsync(customer, "PuzzleSolved", true, store.Id);

        // Find a pre-created discount in nopCommerce admin with coupon code "PUZZLE5"
        var discount = (await _discountService.GetAllDiscountsAsync(couponCode: "PUZZLE5")).FirstOrDefault();

        if (discount != null)
        {
            await _customerService.ApplyDiscountCouponCodeAsync(customer, discount.CouponCode);
            return Json(new { success = true, message = "Discount applied to your cart!" });
        }

        return Json(new { success = false, message = "Discount not found. Please contact support." });
    }

    [HttpPost]
    public async Task<IActionResult> MarkAsSolved(int productId)
    {
        var customer = await _workContext.GetCurrentCustomerAsync();

        // Get existing solved IDs (e.g., "10,15,22")
        var solvedIdsString = await _genericAttributeService.GetAttributeAsync<string>(customer, "SolvedPuzzleProductIds") ?? "";
        var solvedIds = solvedIdsString.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();

        if (!solvedIds.Contains(productId.ToString()))
        {
            solvedIds.Add(productId.ToString());
            await _genericAttributeService.SaveAttributeAsync(customer, "SolvedPuzzleProductIds", string.Join(",", solvedIds));
            
            // Log that it was solved
            try { 
                var logPath = "C:\\Users\\madde\\source\\repos\\derrickmadden1\\nopCommerce\\src\\Presentation\\Nop.Web\\logs\\puzzle_debug.log";
                System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] Product {productId} marked as solved for Customer {customer.Id}\n"); 
            } catch {}
        }

        return Json(new { success = true });
    }

    [Area(AreaNames.ADMIN)]
    [AuthorizeAdmin]
    [Route("Admin/Puzzle/ConfigureRequirement")]
    public async Task<IActionResult> ConfigureRequirement(int discountId, int? discountRequirementId)
    {
        // Use concrete model to avoid RuntimeBinderException
        var model = new Models.RequirementModel
        {
            DiscountId = discountId,
            RequirementId = discountRequirementId ?? 0
        };
 
        return View("~/Plugins/Widgets.ImagePuzzle/Views/ConfigureRequirement.cshtml", model);
    }

    [HttpPost]
    [Area(AreaNames.ADMIN)]
    [AuthorizeAdmin]
    [AutoValidateAntiforgeryToken]
    [Route("Admin/Puzzle/ConfigureRequirement")]
    [CheckPermission(StandardPermission.Promotions.DISCOUNTS_CREATE_EDIT_DELETE)]
    public async Task<IActionResult> ConfigureRequirement(int discountId, int requirementId)
    {
        var discount = await _discountService.GetDiscountByIdAsync(discountId);
        if (discount == null)
            return Json(new { Errors = new[] { "Discount could not be loaded" } });

        var discountRequirement = await _discountService.GetDiscountRequirementByIdAsync(requirementId);
        if (discountRequirement == null)
        {
            discountRequirement = new DiscountRequirement
            {
                DiscountId = discount.Id,
                DiscountRequirementRuleSystemName = "Widgets.ImagePuzzle"
            };
            await _discountService.InsertDiscountRequirementAsync(discountRequirement);
        }
        else
        {
            // Update existing if needed (though no extra fields here)
            discountRequirement.DiscountId = discount.Id;
            discountRequirement.DiscountRequirementRuleSystemName = "Widgets.ImagePuzzle";
            await _discountService.UpdateDiscountRequirementAsync(discountRequirement);
        }

        // Invalidate the cache for puzzle discount IDs
        await _staticCacheManager.RemoveByPrefixAsync("Nop.Plugin.Widgets.ImagePuzzle");

        return Json(new { NewRequirementId = discountRequirement.Id });
    }
}
