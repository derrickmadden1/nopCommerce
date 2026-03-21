using Microsoft.AspNetCore.Mvc;
using Nop.Core;
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
    private readonly IStoreContext _storeContext;
    private readonly IWorkContext _workContext;

    public PuzzleController(ICustomerService customerService,
        IDiscountService discountService,
        IGenericAttributeService genericAttributeService,
        IStoreContext storeContext,
        IWorkContext workContext)
    {
        _customerService = customerService;
        _discountService = discountService;
        _genericAttributeService = genericAttributeService;
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
    public async Task<IActionResult> MarkAsSolved()
    {
        var customer = await _workContext.GetCurrentCustomerAsync();

        // Save the 'Solved' status to the database for this user session
        await _genericAttributeService.SaveAttributeAsync(customer, "PuzzleSolved", true);

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
    public async Task<IActionResult> ConfigureRequirement(int discountId, int? requirementId, bool unused = true)
    {
        var discount = await _discountService.GetDiscountByIdAsync(discountId);
        if (discount == null)
            return Json(new { Errors = new[] { "Discount could not be loaded" } });

        var discountRequirement = await _discountService.GetDiscountRequirementByIdAsync(requirementId ?? 0);
        if (discountRequirement == null)
        {
            discountRequirement = new DiscountRequirement
            {
                DiscountId = discount.Id,
                DiscountRequirementRuleSystemName = "Widgets.ImagePuzzle"
            };
            await _discountService.InsertDiscountRequirementAsync(discountRequirement);
        }

        return Json(new { NewRequirementId = discountRequirement.Id });
    }
}
