using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Events;
using Nop.Plugin.Misc.ReviewReward.Services;
using Nop.Services.Catalog;
using Nop.Services.Customers;
using Nop.Services.Events;

namespace Nop.Plugin.Misc.ReviewReward.Infrastructure
{
    public class ProductReviewEventConsumer : 
        IConsumer<EntityInsertedEvent<ProductReview>>,
        IConsumer<ProductReviewApprovedEvent>
    {
        private readonly IReviewRewardService _reviewRewardService;
        private readonly ICustomerService _customerService;
        private readonly IProductService _productService;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ProductReviewEventConsumer(
            IReviewRewardService reviewRewardService,
            ICustomerService customerService,
            IProductService productService,
            IHttpContextAccessor httpContextAccessor)
        {
            _reviewRewardService = reviewRewardService;
            _customerService = customerService;
            _productService = productService;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task HandleEventAsync(EntityInsertedEvent<ProductReview> eventMessage)
        {
            if (eventMessage?.Entity == null)
                return;

            await ProcessRewardAsync(eventMessage.Entity);
        }

        public async Task HandleEventAsync(ProductReviewApprovedEvent eventMessage)
        {
            if (eventMessage?.ProductReview == null)
                return;

            await ProcessRewardAsync(eventMessage.ProductReview);
        }

        private async Task ProcessRewardAsync(ProductReview review)
        {
            var customer = await _customerService.GetCustomerByIdAsync(review.CustomerId);
            if (customer == null || await _customerService.IsGuestAsync(customer))
                return;

            var product = await _productService.GetProductByIdAsync(review.ProductId);
            if (product == null)
                return;

            var hasOrdered = await _reviewRewardService.CustomerHasOrderedProductAsync(customer, product);
            if (hasOrdered)
            {
                await _reviewRewardService.GrantRewardAsync(customer, review, null);
                return;
            }

            // Check if market purchase code was supplied in the form
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext?.Request?.HasFormContentType == true)
            {
                var codeText = httpContext.Request.Form["MarketPurchaseCode"].ToString();
                if (!string.IsNullOrWhiteSpace(codeText))
                {
                    var marketCode = await _reviewRewardService.ValidateMarketCodeAsync(codeText);
                    if (marketCode != null)
                    {
                        await _reviewRewardService.GrantRewardAsync(customer, review, marketCode);
                    }
                }
            }
        }
    }
}
