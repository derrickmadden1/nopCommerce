using System.Threading.Tasks;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Discounts;

namespace Nop.Plugin.Misc.ReviewReward.Services
{
    public interface IReviewRewardMessageService
    {
        /// <summary>
        /// Sends the ReviewReward.CouponEarned email to the customer with their earned discount coupon.
        /// </summary>
        Task SendReviewRewardCouponEmailAsync(Customer customer, Product product, Discount discount, int languageId = 0, int storeId = 0);
    }
}
