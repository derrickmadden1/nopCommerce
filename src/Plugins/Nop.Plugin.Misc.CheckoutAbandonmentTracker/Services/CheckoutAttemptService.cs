using System;
using System.Linq;
using System.Threading.Tasks;
using Nop.Core;
using Nop.Data;
using Nop.Plugin.Misc.CheckoutAbandonmentTracker.Domain;

namespace Nop.Plugin.Misc.CheckoutAbandonmentTracker.Services
{
    public class CheckoutAttemptService : ICheckoutAttemptService
    {
        private readonly IRepository<CheckoutAttempt> _checkoutAttemptRepository;

        public CheckoutAttemptService(IRepository<CheckoutAttempt> checkoutAttemptRepository)
        {
            _checkoutAttemptRepository = checkoutAttemptRepository;
        }

        public async Task<CheckoutAttempt> RecordStepAsync(int customerId, string customerGuid, int storeId, CheckoutStep step, decimal? cartTotal = null)
        {
            // "Open" = not yet completed (OrderId null) and not yet flagged abandoned.
            // Guests are tracked purely by CustomerGuid since CustomerId is shared
            // across all anonymous customers in nopCommerce.
            var query = _checkoutAttemptRepository.Table
                .Where(a => a.CustomerGuid == customerGuid
                         && a.StoreId == storeId
                         && a.OrderId == null
                         && !a.IsAbandoned);

            var attempt = await query.OrderByDescending(a => a.LastActivityUtc).FirstOrDefaultAsync();

            var nowUtc = DateTime.UtcNow;

            if (attempt == null)
            {
                attempt = new CheckoutAttempt
                {
                    CustomerId = customerId,
                    CustomerGuid = customerGuid,
                    StoreId = storeId,
                    StartedOnUtc = nowUtc,
                    LastActivityUtc = nowUtc,
                    LastStepReached = step,
                    CartTotal = cartTotal,
                    IsAbandoned = false
                };
                await _checkoutAttemptRepository.InsertAsync(attempt);
            }
            else
            {
                // Only move the step forward — a customer navigating back to an
                // earlier step (e.g. "edit shipping address") shouldn't regress
                // the recorded progress.
                if (step > attempt.LastStepReached)
                    attempt.LastStepReached = step;

                attempt.LastActivityUtc = nowUtc;
                if (cartTotal.HasValue)
                    attempt.CartTotal = cartTotal;

                await _checkoutAttemptRepository.UpdateAsync(attempt);
            }

            return attempt;
        }

        public async Task MarkCompletedAsync(int customerId, int orderId)
        {
            // Match on CustomerId here rather than CustomerGuid, since by the time
            // OrderPlacedEvent fires the customer may have been converted from
            // guest to registered — CustomerId is reliably set on the placed
            // order itself.
            var attempt = await _checkoutAttemptRepository.Table
                .Where(a => a.CustomerId == customerId && a.OrderId == null && !a.IsAbandoned)
                .OrderByDescending(a => a.LastActivityUtc)
                .FirstOrDefaultAsync();

            if (attempt == null)
                return; // nothing open to reconcile — fine, not every order needs a matching attempt

            attempt.OrderId = orderId;
            attempt.LastActivityUtc = DateTime.UtcNow;
            await _checkoutAttemptRepository.UpdateAsync(attempt);
        }

        public async Task<int> FlagAbandonedAsync(DateTime cutoffUtc)
        {
            var staleAttempts = await _checkoutAttemptRepository.Table
                .Where(a => a.OrderId == null && !a.IsAbandoned && a.LastActivityUtc < cutoffUtc)
                .ToListAsync();

            foreach (var attempt in staleAttempts)
                attempt.IsAbandoned = true;

            if (staleAttempts.Count > 0)
                await _checkoutAttemptRepository.UpdateAsync(staleAttempts);

            return staleAttempts.Count;
        }

        public async Task<IPagedList<CheckoutAttempt>> GetAbandonedAttemptsAsync(int storeId, int pageIndex = 0, int pageSize = 50)
        {
            var query = _checkoutAttemptRepository.Table
                .Where(a => a.StoreId == storeId && a.IsAbandoned)
                .OrderByDescending(a => a.LastActivityUtc);

            return await query.ToPagedListAsync(pageIndex, pageSize);
        }
    }
}
