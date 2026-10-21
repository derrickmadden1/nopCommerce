using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Plugin.Misc.ReviewReward.Domain;
using Nop.Plugin.Misc.ReviewReward.Models;
using Nop.Plugin.Misc.ReviewReward.Services;
using Nop.Services.Catalog;
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Web.Framework.Components;
using Nop.Web.Framework.Infrastructure;
using Nop.Web.Models.Catalog;

namespace Nop.Plugin.Misc.ReviewReward.Components
{
    public class ReviewRewardReviewFormViewComponent : NopViewComponent
    {
        private readonly IWorkContext _workContext;
        private readonly ICustomerService _customerService;
        private readonly IProductService _productService;
        private readonly ISettingService _settingService;
        private readonly IReviewRewardService _reviewRewardService;

        public ReviewRewardReviewFormViewComponent(
            IWorkContext workContext,
            ICustomerService customerService,
            IProductService productService,
            ISettingService settingService,
            IReviewRewardService reviewRewardService)
        {
            _workContext = workContext;
            _customerService = customerService;
            _productService = productService;
            _settingService = settingService;
            _reviewRewardService = reviewRewardService;
        }

        public async Task<IViewComponentResult> InvokeAsync(string widgetZone, object? additionalData = null)
        {
            // Skip bottom zone if inside-form zone is invoked to avoid double rendering
            if (widgetZone == PublicWidgetZones.ProductReviewsPageBottom)
                return Content(string.Empty);

            var customer = await _workContext.GetCurrentCustomerAsync();
            if (customer == null || !await _customerService.IsRegisteredAsync(customer))
                return Content(string.Empty);

            int productId = 0;
            if (additionalData is ProductReviewsModel reviewsModel)
            {
                productId = reviewsModel.ProductId;
            }
            else if (additionalData is int id)
            {
                productId = id;
            }

            if (productId == 0)
                return Content(string.Empty);

            var product = await _productService.GetProductByIdAsync(productId);
            if (product == null)
                return Content(string.Empty);

            var hasOrdered = await _reviewRewardService.CustomerHasOrderedProductAsync(customer, product);
            var settings = await _settingService.LoadSettingAsync<ReviewRewardSettings>();

            string? freeProductName = null;
            bool offerFreeProduct = false;
            if (settings.OfferFreeProduct && settings.FreeProductId > 0)
            {
                var freeProduct = await _productService.GetProductByIdAsync(settings.FreeProductId);
                if (freeProduct != null && !freeProduct.Deleted && freeProduct.Published)
                {
                    offerFreeProduct = true;
                    freeProductName = freeProduct.Name;
                }
            }

            var model = new ReviewRewardFormModel
            {
                ProductId = productId,
                HasOrderedProduct = hasOrdered,
                OfferFreeProduct = offerFreeProduct,
                FreeProductName = freeProductName
            };

            return View("~/Plugins/Misc.ReviewReward/Views/ReviewReward/ReviewFormRewardInfo.cshtml", model);
        }
    }
}
