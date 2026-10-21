using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Data;
using Nop.Plugin.Misc.ReviewReward.Services;
using Nop.Services.Customers;
using Nop.Services.Orders;
using Nop.Web.Factories;
using Nop.Web.Models.ShoppingCart;

namespace Nop.Plugin.Misc.ReviewReward.Infrastructure
{
    /// <summary>
    /// Synchronizes Free Product Review Reward items in the customer's shopping cart
    /// when a Review Reward coupon code is removed or manually applied in the Shopping Cart.
    /// </summary>
    public class ReviewRewardCartActionFilter : IAsyncActionFilter
    {
        private readonly IWorkContext _workContext;
        private readonly IStoreContext _storeContext;
        private readonly ICustomerService _customerService;
        private readonly IShoppingCartService _shoppingCartService;
        private readonly IShoppingCartModelFactory _shoppingCartModelFactory;
        private readonly IReviewRewardService _reviewRewardService;

        public ReviewRewardCartActionFilter(
            IWorkContext workContext,
            IStoreContext storeContext,
            ICustomerService customerService,
            IShoppingCartService shoppingCartService,
            IShoppingCartModelFactory shoppingCartModelFactory,
            IReviewRewardService reviewRewardService)
        {
            _workContext = workContext;
            _storeContext = storeContext;
            _customerService = customerService;
            _shoppingCartService = shoppingCartService;
            _shoppingCartModelFactory = shoppingCartModelFactory;
            _reviewRewardService = reviewRewardService;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (!DataSettingsManager.IsDatabaseInstalled())
            {
                await next();
                return;
            }

            var controllerName = context.RouteData.Values["controller"]?.ToString();
            if (!string.Equals(controllerName, "ShoppingCart", StringComparison.OrdinalIgnoreCase)
                || !HttpMethods.IsPost(context.HttpContext.Request.Method)
                || !context.HttpContext.Request.HasFormContentType)
            {
                await next();
                return;
            }

            var form = await context.HttpContext.Request.ReadFormAsync();

            // 1. Check if customer is removing a discount coupon code ("removediscount-{id}")
            var removeDiscountKey = form.Keys.FirstOrDefault(k =>
                k.StartsWith("removediscount-", StringComparison.InvariantCultureIgnoreCase));

            if (removeDiscountKey != null &&
                int.TryParse(removeDiscountKey["removediscount-".Length..], out var removedDiscountId) &&
                removedDiscountId > 0)
            {
                var customer = await _workContext.GetCurrentCustomerAsync();
                var store = await _storeContext.GetCurrentStoreAsync();

                await _reviewRewardService.HandleCouponRemovedFromCartAsync(customer, removedDiscountId, store.Id);
                await next();
                return;
            }

            // 2. Check if customer is applying a discount coupon code ("applydiscountcouponcode")
            if (form.ContainsKey("applydiscountcouponcode"))
            {
                var couponCode = form["discountcouponcode"].ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(couponCode))
                {
                    var customer = await _workContext.GetCurrentCustomerAsync();
                    var store = await _storeContext.GetCurrentStoreAsync();

                    var codesBefore = await _customerService.ParseAppliedDiscountCouponCodesAsync(customer);
                    var wasAlreadyApplied = codesBefore.Any(c =>
                        c.Equals(couponCode, StringComparison.InvariantCultureIgnoreCase));

                    var executedContext = await next();

                    var codesAfter = await _customerService.ParseAppliedDiscountCouponCodesAsync(customer);
                    var isNowApplied = codesAfter.Any(c =>
                        c.Equals(couponCode, StringComparison.InvariantCultureIgnoreCase));

                    if (isNowApplied && !wasAlreadyApplied)
                    {
                        var addedFreeProduct = await _reviewRewardService.HandleCouponAppliedInCartAsync(customer, couponCode, store.Id);
                        if (addedFreeProduct &&
                            executedContext.Result is ViewResult viewResult &&
                            viewResult.Model is ShoppingCartModel existingModel)
                        {
                            var updatedCart = await _shoppingCartService.GetShoppingCartAsync(customer, ShoppingCartType.ShoppingCart, store.Id);
                            var refreshedModel = new ShoppingCartModel();
                            refreshedModel.DiscountBox.Messages = existingModel.DiscountBox.Messages;
                            refreshedModel.DiscountBox.IsApplied = existingModel.DiscountBox.IsApplied;
                            refreshedModel = await _shoppingCartModelFactory.PrepareShoppingCartModelAsync(refreshedModel, updatedCart);
                            viewResult.ViewData.Model = refreshedModel;
                        }
                    }

                    return;
                }
            }

            await next();
        }
    }
}
