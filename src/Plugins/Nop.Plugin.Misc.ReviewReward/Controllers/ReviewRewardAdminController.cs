using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Discounts;
using Nop.Data;
using Nop.Plugin.Misc.ReviewReward.Domain;
using Nop.Plugin.Misc.ReviewReward.Models;
using Nop.Plugin.Misc.ReviewReward.Services;
using Nop.Services.Catalog;
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Discounts;
using Nop.Services.Messages;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Models.Extensions;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Misc.ReviewReward.Controllers
{
    [Area(AreaNames.ADMIN)]
    [AuthorizeAdmin]
    [AutoValidateAntiforgeryToken]
    public class ReviewRewardAdminController : BasePluginController
    {
        private readonly ISettingService _settingService;
        private readonly IRepository<MarketPurchaseCode> _marketCodeRepository;
        private readonly IRepository<ReviewRewardCoupon> _rewardRepository;
        private readonly IRepository<Discount> _discountRepository;
        private readonly IRepository<ProductReview> _productReviewRepository;
        private readonly IProductService _productService;
        private readonly ICustomerService _customerService;
        private readonly IDiscountService _discountService;
        private readonly IReviewRewardService _reviewRewardService;
        private readonly INotificationService _notificationService;

        public ReviewRewardAdminController(
            ISettingService settingService,
            IRepository<MarketPurchaseCode> marketCodeRepository,
            IRepository<ReviewRewardCoupon> rewardRepository,
            IRepository<Discount> discountRepository,
            IRepository<ProductReview> productReviewRepository,
            IProductService productService,
            ICustomerService customerService,
            IDiscountService discountService,
            IReviewRewardService reviewRewardService,
            INotificationService notificationService)
        {
            _settingService = settingService;
            _marketCodeRepository = marketCodeRepository;
            _rewardRepository = rewardRepository;
            _discountRepository = discountRepository;
            _productReviewRepository = productReviewRepository;
            _productService = productService;
            _customerService = customerService;
            _discountService = discountService;
            _reviewRewardService = reviewRewardService;
            _notificationService = notificationService;
        }

        [HttpGet]
        public async Task<IActionResult> Configure()
        {
            var settings = await _settingService.LoadSettingAsync<ReviewRewardSettings>();

            var model = new ReviewRewardConfigureModel
            {
                RewardAmount = settings.RewardAmount,
                UsePercentage = settings.UsePercentage,
                CouponPrefix = settings.CouponPrefix,
                ExpiryDays = settings.ExpiryDays
            };

            return View("~/Plugins/Misc.ReviewReward/Views/ReviewRewardAdmin/Configure.cshtml", model);
        }

        [HttpPost]
        public async Task<IActionResult> Configure(ReviewRewardConfigureModel model)
        {
            if (!ModelState.IsValid)
                return await Configure();

            var settings = await _settingService.LoadSettingAsync<ReviewRewardSettings>();
            settings.RewardAmount = model.RewardAmount;
            settings.UsePercentage = model.UsePercentage;
            settings.CouponPrefix = model.CouponPrefix ?? "RVW-";
            settings.ExpiryDays = model.ExpiryDays;

            await _settingService.SaveSettingAsync(settings);

            _notificationService.SuccessNotification("Review Reward settings saved successfully.");

            return RedirectToAction(nameof(Configure));
        }

        [HttpPost]
        public async Task<IActionResult> MarketCodeList(MarketPurchaseCodeSearchModel searchModel)
        {
            var query = _marketCodeRepository.Table.OrderByDescending(c => c.CreatedOnUtc);
            var pagedList = await query.ToPagedListAsync(searchModel.Page - 1, searchModel.PageSize);

            var model = new MarketPurchaseCodeListModel().PrepareToGrid(searchModel, pagedList, () =>
            {
                return pagedList.Select(c => new MarketPurchaseCodeModel
                {
                    Id = c.Id,
                    Code = c.Code,
                    ExpiryDateUtc = c.ExpiryDateUtc,
                    IsActive = c.IsActive,
                    CreatedOnUtc = c.CreatedOnUtc
                });
            });

            return Json(model);
        }

        [HttpPost]
        public async Task<IActionResult> MarketCodeCreate(string code, int daysValid)
        {
            if (string.IsNullOrWhiteSpace(code))
                return Json(new { success = false, message = "Code is required" });

            var trimmedCode = code.Trim().ToUpperInvariant();
            var existing = await _marketCodeRepository.Table.FirstOrDefaultAsync(c => c.Code == trimmedCode);
            if (existing != null)
                return Json(new { success = false, message = "Market code already exists" });

            int validDays = daysValid > 0 ? daysValid : 30;

            var newCode = new MarketPurchaseCode
            {
                Code = trimmedCode,
                ExpiryDateUtc = DateTime.UtcNow.AddDays(validDays),
                IsActive = true,
                CreatedOnUtc = DateTime.UtcNow
            };

            await _marketCodeRepository.InsertAsync(newCode);

            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> MarketCodeToggleStatus(int id)
        {
            var code = await _marketCodeRepository.GetByIdAsync(id);
            if (code == null)
                return Json(new { success = false });

            code.IsActive = !code.IsActive;
            await _marketCodeRepository.UpdateAsync(code);

            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> CouponList(ReviewRewardCouponSearchModel searchModel)
        {
            var query = _rewardRepository.Table;

            if (!string.IsNullOrWhiteSpace(searchModel.SearchCouponCode))
            {
                var searchCode = searchModel.SearchCouponCode.Trim();
                var matchingDiscountIds = _discountRepository.Table
                    .Where(d => d.CouponCode.Contains(searchCode))
                    .Select(d => d.Id);

                query = query.Where(r => matchingDiscountIds.Contains(r.DiscountId));
            }

            var pagedList = await query.OrderByDescending(r => r.CreatedOnUtc)
                .ToPagedListAsync(searchModel.Page - 1, searchModel.PageSize);

            var model = new ReviewRewardCouponListModel().PrepareToGrid(searchModel, pagedList, () =>
            {
                return pagedList.Select(r =>
                {
                    var customer = _customerService.GetCustomerByIdAsync(r.CustomerId).Result;
                    var review = _productReviewRepository.GetByIdAsync(r.ProductReviewId).Result;
                    var product = review != null ? _productService.GetProductByIdAsync(review.ProductId).Result : null;
                    var discount = _discountRepository.GetByIdAsync(r.DiscountId).Result;

                    string amountText = string.Empty;
                    if (discount != null)
                    {
                        amountText = discount.UsePercentage
                            ? $"{discount.DiscountPercentage:G29}%"
                            : $"{discount.DiscountAmount:C2}";
                    }

                    var usageHistoryList = _discountService.GetAllDiscountUsageHistoryAsync(r.DiscountId, null, null, false, 0, 1).Result;
                    var usageHistory = usageHistoryList.FirstOrDefault();

                    bool isRedeemed = r.RedeemedOnUtc.HasValue || usageHistory != null || (discount != null && !discount.IsActive);
                    string? redeemedVia = r.RedeemedVia;
                    DateTime? redeemedOn = r.RedeemedOnUtc;

                    if (usageHistory != null && !r.RedeemedOnUtc.HasValue)
                    {
                        redeemedVia = usageHistory.OrderId > 0 ? $"Online (Order #{usageHistory.OrderId})" : "Online";
                        redeemedOn = usageHistory.CreatedOnUtc;
                    }

                    return new ReviewRewardCouponModel
                    {
                        Id = r.Id,
                        CustomerId = r.CustomerId,
                        CustomerEmail = customer?.Email ?? customer?.Username ?? $"Customer #{r.CustomerId}",
                        ProductReviewId = r.ProductReviewId,
                        ProductName = product?.Name ?? $"Product #{review?.ProductId}",
                        CouponCode = discount?.CouponCode ?? string.Empty,
                        DiscountAmountText = amountText,
                        CreatedOnUtc = r.CreatedOnUtc,
                        RedeemedOnUtc = redeemedOn,
                        RedeemedVia = redeemedVia ?? (isRedeemed ? "Market" : null),
                        IsRedeemed = isRedeemed
                    };
                });
            });

            return Json(model);
        }

        [HttpPost]
        public async Task<IActionResult> MarkRedeemedManually(int id)
        {
            await _reviewRewardService.MarkRedeemedManuallyAsync(id);
            return Json(new { success = true });
        }
    }
}
