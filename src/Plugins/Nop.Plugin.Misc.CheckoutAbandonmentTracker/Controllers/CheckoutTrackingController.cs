using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Plugin.Misc.CheckoutAbandonmentTracker.Domain;
using Nop.Plugin.Misc.CheckoutAbandonmentTracker.Models;
using Nop.Plugin.Misc.CheckoutAbandonmentTracker.Services;
using Nop.Services.Orders;
using Nop.Web.Framework.Controllers;

namespace Nop.Plugin.Misc.CheckoutAbandonmentTracker.Controllers
{
    public class CheckoutTrackingController : BasePluginController
    {
        private readonly ICheckoutAttemptService _checkoutAttemptService;
        private readonly IWorkContext _workContext;
        private readonly IStoreContext _storeContext;
        private readonly IShoppingCartService _shoppingCartService;
        private readonly IOrderTotalCalculationService _orderTotalCalculationService;

        public CheckoutTrackingController(
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

        // All three express-payment buttons (PayPal wallet, Card, GPay) render
        // through PayPalCommerce's single SDK call and share the same onClick
        // hook client-side, so they all map to the same server-side step here —
        // the distinct event names are still preserved in Clarity/GTM for
        // funnel breakdown by button type.
        private static readonly HashSet<string> RecognizedEvents = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "checkout_paypal_express_clicked",
            "checkout_card_express_clicked",
            "checkout_gpay_clicked"
        };

        [HttpPost]
        [IgnoreAntiforgeryToken] // fired from a PayPal SDK callback context, not a standard form post
        public async Task<IActionResult> RecordEvent([FromBody] CheckoutTrackingEventModel model)
        {
            // Whitelist explicitly — never let arbitrary client input choose the
            // step, since this endpoint is unauthenticated by necessity.
            if (string.IsNullOrEmpty(model?.EventName) || !RecognizedEvents.Contains(model.EventName))
                return BadRequest();

            var step = CheckoutStep.ExpressCheckoutClicked;

            var customer = await _workContext.GetCurrentCustomerAsync();
            var store = await _storeContext.GetCurrentStoreAsync();

            decimal? cartTotal = null;
            try
            {
                var cart = await _shoppingCartService.GetShoppingCartAsync(customer, ShoppingCartType.ShoppingCart, store.Id);
                cartTotal = (await _orderTotalCalculationService.GetShoppingCartTotalAsync(cart)).shoppingCartTotal;
            }
            catch
            {
                // non-critical — don't block on tracking
            }

            await _checkoutAttemptService.RecordStepAsync(
                customer.Id, customer.CustomerGuid.ToString(), store.Id, step, cartTotal);

            return Ok();
        }
    }
}

