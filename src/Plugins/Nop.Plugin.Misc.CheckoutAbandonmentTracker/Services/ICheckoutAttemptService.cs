using System;
using System.Threading.Tasks;
using Nop.Core;
using Nop.Plugin.Misc.CheckoutAbandonmentTracker.Domain;

namespace Nop.Plugin.Misc.CheckoutAbandonmentTracker.Services
{
    public interface ICheckoutAttemptService
    {
        Task<CheckoutAttempt> RecordStepAsync(int customerId, string customerGuid, int storeId, CheckoutStep step, decimal? cartTotal = null);
        Task MarkCompletedAsync(int customerId, int orderId);
        Task<int> FlagAbandonedAsync(DateTime cutoffUtc);
        Task<IPagedList<CheckoutAttempt>> GetAbandonedAttemptsAsync(int storeId, int pageIndex = 0, int pageSize = 50);
    }
}
