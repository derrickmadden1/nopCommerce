using System;
using System.Threading.Tasks;
using Nop.Services.ScheduleTasks;

namespace Nop.Plugin.Misc.CheckoutAbandonmentTracker.Services
{
    public class AbandonedCheckoutTask : IScheduleTask
    {
        private readonly ICheckoutAttemptService _checkoutAttemptService;

        public AbandonedCheckoutTask(ICheckoutAttemptService checkoutAttemptService)
        {
            _checkoutAttemptService = checkoutAttemptService;
        }

        public async Task ExecuteAsync()
        {
            var cutoff = DateTime.UtcNow.AddMinutes(-45); // tune this threshold
            await _checkoutAttemptService.FlagAbandonedAsync(cutoff);
        }
    }
}
