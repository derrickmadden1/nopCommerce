using System.Threading.Tasks;
using Nop.Core.Domain.Orders;
using Nop.Services.Events;

namespace Nop.Plugin.Misc.CheckoutAbandonmentTracker.Services
{
    public class OrderPlacedConsumer : IConsumer<OrderPlacedEvent>
    {
        private readonly ICheckoutAttemptService _checkoutAttemptService;

        public OrderPlacedConsumer(ICheckoutAttemptService checkoutAttemptService)
        {
            _checkoutAttemptService = checkoutAttemptService;
        }

        public async Task HandleEventAsync(OrderPlacedEvent eventMessage)
        {
            var order = eventMessage.Order;
            await _checkoutAttemptService.MarkCompletedAsync(order.CustomerId, order.Id);
        }
    }
}

