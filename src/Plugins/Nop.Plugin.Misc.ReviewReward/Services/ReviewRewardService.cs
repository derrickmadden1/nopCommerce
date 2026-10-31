using System;
using System.Linq;
using System.Threading.Tasks;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Discounts;
using Nop.Core.Domain.Orders;
using Nop.Data;
using Nop.Plugin.Misc.ReviewReward.Domain;
using Nop.Services.Catalog;
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Discounts;
using Nop.Services.Orders;

namespace Nop.Plugin.Misc.ReviewReward.Services
{
    public class ReviewRewardService : IReviewRewardService
    {
        private readonly IRepository<MarketPurchaseCode> _marketCodeRepository;
        private readonly IRepository<MarketCodeUsage> _marketCodeUsageRepository;
        private readonly IRepository<ReviewRewardCoupon> _rewardRepository;
        private readonly IRepository<Discount> _discountRepository;
        private readonly IRepository<DiscountProductMapping> _discountProductMappingRepository;
        private readonly IOrderService _orderService;
        private readonly IProductService _productService;
        private readonly IDiscountService _discountService;
        private readonly ICustomerService _customerService;
        private readonly IShoppingCartService _shoppingCartService;
        private readonly IStoreContext _storeContext;
        private readonly ISettingService _settingService;
        private readonly IReviewRewardMessageService _reviewRewardMessageService;

        public ReviewRewardService(
            IRepository<MarketPurchaseCode> marketCodeRepository,
            IRepository<MarketCodeUsage> marketCodeUsageRepository,
            IRepository<ReviewRewardCoupon> rewardRepository,
            IRepository<Discount> discountRepository,
            IRepository<DiscountProductMapping> discountProductMappingRepository,
            IOrderService orderService,
            IProductService productService,
            IDiscountService discountService,
            ICustomerService customerService,
            IShoppingCartService shoppingCartService,
            IStoreContext storeContext,
            ISettingService settingService,
            IReviewRewardMessageService reviewRewardMessageService)
        {
            _marketCodeRepository = marketCodeRepository;
            _marketCodeUsageRepository = marketCodeUsageRepository;
            _rewardRepository = rewardRepository;
            _discountRepository = discountRepository;
            _discountProductMappingRepository = discountProductMappingRepository;
            _orderService = orderService;
            _productService = productService;
            _discountService = discountService;
            _customerService = customerService;
            _shoppingCartService = shoppingCartService;
            _storeContext = storeContext;
            _settingService = settingService;
            _reviewRewardMessageService = reviewRewardMessageService;
        }

        public async Task<bool> CustomerHasOrderedProductAsync(Customer customer, Product product)
        {
            var orders = await _orderService.SearchOrdersAsync(customerId: customer.Id);
            foreach (var order in orders)
            {
                var items = await _orderService.GetOrderItemsAsync(order.Id, isNotReturnable: false);
                if (items.Any(i => i.ProductId == product.Id))
                    return true;
            }
            return false;
        }

        public async Task<MarketPurchaseCode?> ValidateMarketCodeAsync(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return null;

            var trimmedCode = code.Trim().ToUpperInvariant();
            var match = await _marketCodeRepository.Table
                .FirstOrDefaultAsync(c => c.Code == trimmedCode && c.IsActive);

            if (match == null || match.ExpiryDateUtc < DateTime.UtcNow)
                return null;

            return match;
        }

        public async Task<Product?> GetFreeProductForDiscountAsync(int discountId)
        {
            if (discountId <= 0)
                return null;

            var discount = await _discountService.GetDiscountByIdAsync(discountId);
            if (discount == null || discount.DiscountType != DiscountType.AssignedToSkus)
                return null;

            var mapping = await _discountProductMappingRepository.Table
                .FirstOrDefaultAsync(m => m.DiscountId == discountId);
            if (mapping == null)
                return null;

            return await _productService.GetProductByIdAsync(mapping.EntityId);
        }

        public async Task<bool> HasActiveReviewRewardCouponAppliedAsync(Customer customer, int? excludeDiscountId = null)
        {
            if (customer == null)
                return false;

            var storeId = (await _storeContext.GetCurrentStoreAsync())?.Id ?? 0;
            var cart = await _shoppingCartService.GetShoppingCartAsync(customer, ShoppingCartType.ShoppingCart, storeId);
            if (cart != null && !cart.Any())
                return false;

            var appliedCouponCodes = await _customerService.ParseAppliedDiscountCouponCodesAsync(customer);
            if (appliedCouponCodes == null || appliedCouponCodes.Length == 0)
                return false;

            var normalizedCodes = appliedCouponCodes
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .SelectMany(c => new[] { c.Trim(), c.Trim().ToUpperInvariant(), c.Trim().ToLowerInvariant() })
                .Distinct()
                .ToList();

            var now = DateTime.UtcNow;
            var candidateDiscounts = await _discountRepository.Table
                .Where(d => normalizedCodes.Contains(d.CouponCode)
                    && d.IsActive
                    && (!excludeDiscountId.HasValue || d.Id != excludeDiscountId.Value)
                    && (!d.EndDateUtc.HasValue || d.EndDateUtc.Value > now))
                .ToListAsync();

            if (!candidateDiscounts.Any())
                return false;

            var candidateDiscountIds = candidateDiscounts.Select(d => d.Id).ToList();
            var unredeemedRewards = await _rewardRepository.Table
                .Where(r => candidateDiscountIds.Contains(r.DiscountId) && !r.RedeemedOnUtc.HasValue)
                .ToListAsync();

            foreach (var reward in unredeemedRewards)
            {
                var usageHistory = await _discountService.GetAllDiscountUsageHistoryAsync(reward.DiscountId, null, null, false, 0, 1);
                if (usageHistory.TotalCount == 0)
                    return true;
            }

            return false;
        }

        public async Task<ReviewRewardCoupon> GrantRewardAsync(Customer customer, ProductReview review,
            MarketPurchaseCode? marketCodeUsed = null)
        {
            var existingReward = await _rewardRepository.Table.FirstOrDefaultAsync(r => r.ProductReviewId == review.Id);
            if (existingReward != null)
                return existingReward;

            var settings = await _settingService.LoadSettingAsync<ReviewRewardSettings>();

            var now = DateTime.UtcNow;
            DateTime? endDate = settings.ExpiryDays > 0 ? now.AddDays(settings.ExpiryDays) : null;
            var prefix = string.IsNullOrWhiteSpace(settings.CouponPrefix) ? "RVW-" : settings.CouponPrefix.Trim();

            Product? freeProduct = null;
            if (settings.OfferFreeProduct && settings.FreeProductId > 0)
            {
                var candidate = await _productService.GetProductByIdAsync(settings.FreeProductId);
                if (candidate != null && !candidate.Deleted && candidate.Published)
                {
                    freeProduct = candidate;
                }
            }

            Discount discount;
            if (freeProduct != null)
            {
                discount = new Discount
                {
                    Name = $"Review reward (Free Product: {freeProduct.Name}) - customer {customer.Id} - review {review.Id}",
                    DiscountTypeId = (int)DiscountType.AssignedToSkus,
                    UsePercentage = true,
                    DiscountPercentage = 100m,
                    DiscountAmount = 0m,
                    MaximumDiscountedQuantity = 1,
                    RequiresCouponCode = true,
                    CouponCode = prefix + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
                    DiscountLimitationId = (int)DiscountLimitationType.NTimesOnly,
                    LimitationTimes = 1,
                    StartDateUtc = now,
                    EndDateUtc = endDate,
                    IsActive = true
                };
                await _discountService.InsertDiscountAsync(discount);

                await _productService.InsertDiscountProductMappingAsync(new DiscountProductMapping
                {
                    DiscountId = discount.Id,
                    EntityId = freeProduct.Id
                });
                await _productService.UpdateProductAsync(freeProduct);
            }
            else
            {
                discount = new Discount
                {
                    Name = $"Review reward - customer {customer.Id} - review {review.Id}",
                    DiscountTypeId = (int)DiscountType.AssignedToOrderTotal,
                    UsePercentage = settings.UsePercentage,
                    DiscountAmount = settings.UsePercentage ? 0 : settings.RewardAmount,
                    DiscountPercentage = settings.UsePercentage ? settings.RewardAmount : 0,
                    RequiresCouponCode = true,
                    CouponCode = prefix + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
                    DiscountLimitationId = (int)DiscountLimitationType.NTimesOnly,
                    LimitationTimes = 1,
                    StartDateUtc = now,
                    EndDateUtc = endDate,
                    IsActive = true
                };
                await _discountService.InsertDiscountAsync(discount);
            }

            // Attach requirement rule to enforce "only 1 review reward coupon per order"
            var requirement = new DiscountRequirement
            {
                DiscountId = discount.Id,
                DiscountRequirementRuleSystemName = "Misc.ReviewReward",
                InteractionType = RequirementGroupInteractionType.And
            };
            await _discountService.InsertDiscountRequirementAsync(requirement);

            var reward = new ReviewRewardCoupon
            {
                CustomerId = customer.Id,
                ProductReviewId = review.Id,
                DiscountId = discount.Id,
                CreatedOnUtc = now
            };
            await _rewardRepository.InsertAsync(reward);

            if (marketCodeUsed != null)
            {
                await _marketCodeUsageRepository.InsertAsync(new MarketCodeUsage
                {
                    MarketPurchaseCodeId = marketCodeUsed.Id,
                    CustomerId = customer.Id,
                    ProductId = review.ProductId,
                    ProductReviewId = review.Id,
                    UsedOnUtc = now
                });
            }

            // If this is a Free Product reward, add the free product directly to the customer's basket
            // and apply its coupon code if no other review reward coupon is currently active in the basket.
            if (freeProduct != null)
            {
                var hasExistingRewardInCart = await HasActiveReviewRewardCouponAppliedAsync(customer, excludeDiscountId: discount.Id);
                if (!hasExistingRewardInCart)
                {
                    var storeId = review.StoreId > 0
                        ? review.StoreId
                        : (await _storeContext.GetCurrentStoreAsync()).Id;

                    var existingCart = await _shoppingCartService.GetShoppingCartAsync(customer, ShoppingCartType.ShoppingCart, storeId);
                    if (existingCart != null && !existingCart.Any())
                    {
                        var staleCodes = await _customerService.ParseAppliedDiscountCouponCodesAsync(customer);
                        if (staleCodes != null)
                        {
                            foreach (var staleCode in staleCodes)
                                await _customerService.RemoveDiscountCouponCodeAsync(customer, staleCode);
                        }
                    }

                    var warnings = await _shoppingCartService.AddToCartAsync(
                        customer: customer,
                        product: freeProduct,
                        shoppingCartType: ShoppingCartType.ShoppingCart,
                        storeId: storeId,
                        quantity: 1);

                    if (!warnings.Any())
                    {
                        await _customerService.ApplyDiscountCouponCodeAsync(customer, discount.CouponCode);
                        await _productService.UpdateProductAsync(freeProduct);
                    }
                }
            }

            // Send reward email to customer
            var product = await _productService.GetProductByIdAsync(review.ProductId);
            await _reviewRewardMessageService.SendReviewRewardCouponEmailAsync(customer, product, discount, storeId: review.StoreId);

            return reward;
        }

        public async Task<bool> HandleCouponAppliedInCartAsync(Customer customer, string couponCode, int storeId)
        {
            if (customer == null || string.IsNullOrWhiteSpace(couponCode))
                return false;

            var trimmed = couponCode.Trim();
            var upper = trimmed.ToUpperInvariant();
            var lower = trimmed.ToLowerInvariant();

            var discount = await _discountRepository.Table
                .FirstOrDefaultAsync(d => (d.CouponCode == trimmed || d.CouponCode == upper || d.CouponCode == lower) && d.IsActive);
            if (discount == null)
                return false;

            var isReviewReward = await _rewardRepository.Table.AnyAsync(r => r.DiscountId == discount.Id && !r.RedeemedOnUtc.HasValue);
            if (!isReviewReward)
                return false;

            var freeProduct = await GetFreeProductForDiscountAsync(discount.Id);
            if (freeProduct == null || freeProduct.Deleted || !freeProduct.Published)
                return false;

            var warnings = await _shoppingCartService.AddToCartAsync(
                customer: customer,
                product: freeProduct,
                shoppingCartType: ShoppingCartType.ShoppingCart,
                storeId: storeId,
                quantity: 1);

            await _customerService.ApplyDiscountCouponCodeAsync(customer, discount.CouponCode);
            await _productService.UpdateProductAsync(freeProduct);

            return !warnings.Any();
        }

        public async Task<bool> HandleCouponRemovedFromCartAsync(Customer customer, int discountId, int storeId)
        {
            if (customer == null || discountId <= 0)
                return false;

            var isReviewReward = await _rewardRepository.Table.AnyAsync(r => r.DiscountId == discountId);
            if (!isReviewReward)
                return false;

            var discount = await _discountService.GetDiscountByIdAsync(discountId);
            if (discount == null || string.IsNullOrWhiteSpace(discount.CouponCode))
                return false;

            var appliedCodes = await _customerService.ParseAppliedDiscountCouponCodesAsync(customer);
            var wasApplied = appliedCodes.Any(c => c.Equals(discount.CouponCode, StringComparison.InvariantCultureIgnoreCase));
            if (!wasApplied)
                return false;

            // Remove the coupon code first so CartItemDeleted event won't re-enter
            await _customerService.RemoveDiscountCouponCodeAsync(customer, discount.CouponCode);

            var freeProduct = await GetFreeProductForDiscountAsync(discountId);
            if (freeProduct == null)
                return false;

            var cart = await _shoppingCartService.GetShoppingCartAsync(customer, ShoppingCartType.ShoppingCart, storeId);
            var cartItem = cart.FirstOrDefault(sci => sci.ProductId == freeProduct.Id);
            if (cartItem != null)
            {
                if (cartItem.Quantity > 1)
                {
                    await _shoppingCartService.UpdateShoppingCartItemAsync(
                        customer,
                        cartItem.Id,
                        cartItem.AttributesXml,
                        cartItem.CustomerEnteredPrice,
                        cartItem.RentalStartDateUtc,
                        cartItem.RentalEndDateUtc,
                        cartItem.Quantity - 1,
                        true);
                }
                else
                {
                    await _shoppingCartService.DeleteShoppingCartItemAsync(cartItem);
                }
            }

            await _productService.UpdateProductAsync(freeProduct);
            return true;
        }

        public async Task HandleCartItemDeletedAsync(ShoppingCartItem deletedItem)
        {
            if (deletedItem == null || deletedItem.ShoppingCartType != ShoppingCartType.ShoppingCart)
                return;

            var customer = await _customerService.GetCustomerByIdAsync(deletedItem.CustomerId);
            if (customer == null)
                return;

            var remainingCart = await _shoppingCartService.GetShoppingCartAsync(customer, ShoppingCartType.ShoppingCart, deletedItem.StoreId);
            if (remainingCart.Any(sci => sci.ProductId == deletedItem.ProductId))
                return;

            var appliedCodes = await _customerService.ParseAppliedDiscountCouponCodesAsync(customer);
            if (appliedCodes == null || appliedCodes.Length == 0)
                return;

            var normalizedCodes = appliedCodes
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .SelectMany(c => new[] { c.Trim(), c.Trim().ToUpperInvariant(), c.Trim().ToLowerInvariant() })
                .Distinct()
                .ToList();

            var appliedDiscounts = await _discountRepository.Table
                .Where(d => normalizedCodes.Contains(d.CouponCode) && d.DiscountTypeId == (int)DiscountType.AssignedToSkus)
                .ToListAsync();

            foreach (var discount in appliedDiscounts)
            {
                var isReviewReward = await _rewardRepository.Table.AnyAsync(r => r.DiscountId == discount.Id);
                if (!isReviewReward)
                    continue;

                var freeProduct = await GetFreeProductForDiscountAsync(discount.Id);
                if (freeProduct != null && freeProduct.Id == deletedItem.ProductId)
                {
                    await _customerService.RemoveDiscountCouponCodeAsync(customer, discount.CouponCode);
                    await _productService.UpdateProductAsync(freeProduct);
                }
            }
        }

        public async Task HandleOrderPlacedAsync(Order order)
        {
            if (order == null)
                return;

            var usageHistoryList = await _discountService.GetAllDiscountUsageHistoryAsync(orderId: order.Id);
            foreach (var usage in usageHistoryList)
            {
                var reward = await _rewardRepository.Table.FirstOrDefaultAsync(r => r.DiscountId == usage.DiscountId);
                if (reward != null && !reward.RedeemedOnUtc.HasValue)
                {
                    reward.RedeemedOnUtc = DateTime.UtcNow;
                    reward.RedeemedVia = $"Online (Order #{order.Id})";
                    await _rewardRepository.UpdateAsync(reward);
                }
            }
        }

        public async Task MarkRedeemedManuallyAsync(int reviewRewardCouponId)
        {
            var reward = await _rewardRepository.GetByIdAsync(reviewRewardCouponId);
            if (reward == null || reward.RedeemedOnUtc.HasValue)
                return;

            var discount = await _discountRepository.GetByIdAsync(reward.DiscountId);

            // If this coupon was currently applied in the customer's online basket, remove it (and any free product)
            var customer = await _customerService.GetCustomerByIdAsync(reward.CustomerId);
            if (customer != null && discount != null)
            {
                var store = await _storeContext.GetCurrentStoreAsync();
                await HandleCouponRemovedFromCartAsync(customer, discount.Id, store.Id);
            }

            reward.RedeemedOnUtc = DateTime.UtcNow;
            reward.RedeemedVia = "Market";
            await _rewardRepository.UpdateAsync(reward);

            if (discount != null)
            {
                // Deactivate the discount so it can no longer be used online
                discount.IsActive = false;
                await _discountService.UpdateDiscountAsync(discount);

                await _discountService.InsertDiscountUsageHistoryAsync(new DiscountUsageHistory
                {
                    DiscountId = discount.Id,
                    CreatedOnUtc = DateTime.UtcNow
                });
            }
        }
    }
}
