using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Filters;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Plugin.Misc.CheckoutAbandonmentTracker.Domain;
using Nop.Plugin.Misc.CheckoutAbandonmentTracker.Services;
using Nop.Services.Orders;

namespace Nop.Plugin.Misc.CheckoutAbandonmentTracker.Infrastructure
{
    public class CheckoutAttemptFilter : IAsyncActionFilter
    {
        private readonly ICheckoutAttemptService _checkoutAttemptService;
        private readonly IWorkContext _workContext;
        private readonly IStoreContext _storeContext;
        private readonly IShoppingCartService _shoppingCartService;
        private readonly IOrderTotalCalculationService _orderTotalCalculationService;

        public CheckoutAttemptFilter(
            ICheckoutAttemptService checkoutAttemptService,
            IWorkContext workContext,
            IStoreContext storeContext,
            IShoppingCartService shoppingCartService,
            IOrderTotalCalculationService orderTotalCalculationService)
        {
            _checkoutAttemptService = checkoutAttemptService;
            _workContext = workContext;
            _storeContext = storeContext;
            _shoppingCartService = shoppingCartService;
            _orderTotalCalculationService = orderTotalCalculationService;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var executedContext = await next();

            if (executedContext.Exception != null && !executedContext.ExceptionHandled)
                return;

            var controllerName = context.RouteData.Values["controller"]?.ToString() ?? string.Empty;
            var actionName = context.RouteData.Values["action"]?.ToString() ?? string.Empty;
            var httpMethod = context.HttpContext.Request.Method;

            var step = MapToStep(controllerName, actionName, httpMethod);
            if (step == null)
                return;

            var customer = await _workContext.GetCurrentCustomerAsync();
            var store = await _storeContext.GetCurrentStoreAsync();

            // Skip logging entirely for customers with an empty cart hitting the
            // cart page — otherwise every casual browse of an empty cart page
            // creates a row.
            var cart = await _shoppingCartService.GetShoppingCartAsync(customer, ShoppingCartType.ShoppingCart, store.Id);
            if (step == CheckoutStep.CartViewed && !cart.Any())
                return;

            decimal? cartTotal = null;
            try
            {
                cartTotal = (await _orderTotalCalculationService.GetShoppingCartTotalAsync(cart)).shoppingCartTotal;
            }
            catch
            {
                // Total calculation can legitimately throw mid-checkout (e.g. no
                // shipping method selected yet) — don't let a tracking failure
                // break the actual checkout flow.
            }

            await _checkoutAttemptService.RecordStepAsync(
                customer.Id,
                customer.CustomerGuid.ToString(),
                store.Id,
                step.Value,
                cartTotal);
        }

        // GET-arrival based mapping: tracks how far a customer actually reached
        // (page load), not just which forms they successfully submitted. See
        // README for why this was chosen over POST-only tracking.
        private static CheckoutStep? MapToStep(string controller, string action, string httpMethod)
        {
            if (controller == "ShoppingCart" && action == "Cart" && httpMethod == "GET")
                return CheckoutStep.CartViewed;

            if (controller == "Checkout")
            {
                return (action, httpMethod) switch
                {
                    ("Index", "GET") => CheckoutStep.CheckoutStarted,
                    ("BillingAddress", "GET") => CheckoutStep.BillingAddress,
                    ("ShippingAddress", "GET") => CheckoutStep.ShippingAddress,
                    ("PaymentMethod", "GET") => CheckoutStep.PaymentMethod,
                    ("Confirm", "GET") => CheckoutStep.Confirmed,
                    _ => (CheckoutStep?)null
                };
            }

            return null;
        }
    }
}

