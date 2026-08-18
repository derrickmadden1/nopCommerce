using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Plugin.Misc.CheckoutAbandonmentTracker.Models;
using Nop.Plugin.Misc.CheckoutAbandonmentTracker.Services;
using Nop.Services.Customers;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Models.Extensions;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Misc.CheckoutAbandonmentTracker.Controllers
{
    [Area(AreaNames.ADMIN)]
    [AuthorizeAdmin]
    [AutoValidateAntiforgeryToken]
    public class CheckoutAbandonmentTrackerController : BasePluginController
    {
        private readonly ICheckoutAttemptService _checkoutAttemptService;
        private readonly ICustomerService _customerService;
        private readonly IStoreContext _storeContext;
        private readonly IPermissionService _permissionService;

        public CheckoutAbandonmentTrackerController(
            ICheckoutAttemptService checkoutAttemptService,
            ICustomerService customerService,
            IStoreContext storeContext,
            IPermissionService permissionService)
        {
            _checkoutAttemptService = checkoutAttemptService;
            _customerService = customerService;
            _storeContext = storeContext;
            _permissionService = permissionService;
        }

        public async Task<IActionResult> List()
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermission.Configuration.MANAGE_PLUGINS))
                return AccessDeniedView();

            return View("~/Plugins/Misc.CheckoutAbandonmentTracker/Views/List.cshtml", new CheckoutAttemptSearchModel());
        }

        [HttpPost]
        public async Task<IActionResult> ListData(CheckoutAttemptSearchModel searchModel)
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermission.Configuration.MANAGE_PLUGINS))
                return await AccessDeniedJsonAsync();

            var store = await _storeContext.GetCurrentStoreAsync();

            var attempts = await _checkoutAttemptService.GetAbandonedAttemptsAsync(
                store.Id,
                searchModel.Page - 1,
                searchModel.PageSize);

            var model = await new CheckoutAttemptListModel().PrepareToGridAsync(searchModel, attempts, () =>
            {
                return attempts.SelectAwait(async attempt =>
                {
                    var customer = attempt.CustomerId.HasValue
                        ? await _customerService.GetCustomerByIdAsync(attempt.CustomerId.Value)
                        : null;

                    var isGuest = customer == null || !await _customerService.IsRegisteredAsync(customer);

                    return new CheckoutAttemptModel
                    {
                        Id = attempt.Id,
                        CustomerId = attempt.CustomerId,
                        CustomerEmail = isGuest ? "(guest)" : customer?.Email,
                        CustomerGuid = attempt.CustomerGuid,
                        LastStepReached = attempt.LastStepReached.ToString(),
                        StartedOnUtc = attempt.StartedOnUtc,
                        LastActivityUtc = attempt.LastActivityUtc,
                        CartTotal = attempt.CartTotal,
                        IsAbandoned = attempt.IsAbandoned,
                        OrderId = attempt.OrderId
                    };
                });
            });

            return Json(model);
        }
    }
}


