using Nop.Core.Events;
using Nop.Plugin.Widgets.MarketLocator.Services;
using Nop.Services.Common;
using Nop.Services.Events;
using Nop.Services.Orders;

namespace Nop.Plugin.Widgets.MarketLocator.Infrastructure;

/// <summary>
/// Listens for OrderPlacedEvent to:
///   1. Snapshot pickup details from the customer session onto the order.
///   2. Clear the temporary checkout selection from the customer attributes.
///
/// This is the correct nopCommerce pattern for post-order processing —
/// no controller hacks needed.
/// </summary>
public class OrderPlacedEventConsumer : IConsumer<OrderPlacedEvent>
{
    private readonly IMarketPickupService _pickupService;
    private readonly IGenericAttributeService _genericAttributeService;
    private readonly IOrderService _orderService;

    public OrderPlacedEventConsumer(
        IMarketPickupService pickupService,
        IGenericAttributeService genericAttributeService,
        IOrderService orderService)
    {
        _pickupService = pickupService;
        _genericAttributeService = genericAttributeService;
        _orderService = orderService;
    }

    public async Task HandleEventAsync(OrderPlacedEvent eventMessage)
    {
        var order = eventMessage.Order;
        if (order is null) return;

        // Only process if the customer chose Market Pickup as shipping method.
        if (!IsMarketPickupOrder(order)) return;

        // Stamp details onto the order.
        await _pickupService.StampOrderAsync(order);

        // Clear the temporary selection from the customer record.
        var customer = new Nop.Core.Domain.Customers.Customer { Id = order.CustomerId };
        await _pickupService.ClearSelectionAsync(customer);
    }

    private static bool IsMarketPickupOrder(Nop.Core.Domain.Orders.Order order) =>
        order.ShippingRateComputationMethodSystemName ==
            Shipping.MarketPickupShippingPlugin.SystemName;
}
