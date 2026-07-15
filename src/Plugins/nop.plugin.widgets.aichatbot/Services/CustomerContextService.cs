using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Plugin.Widgets.AiChatbot.Models;
using Nop.Services.Catalog;
using Nop.Services.Customers;
using Nop.Services.Orders;
using Nop.Services.Shipping;

namespace Nop.Plugin.Widgets.AiChatbot.Services;

/// <summary>
/// Builds rich customer context for injection into the system prompt.
/// </summary>
public class CustomerContextService
{
    private readonly IWorkContext _workContext;
    private readonly ICustomerService _customerService;
    private readonly IOrderService _orderService;
    private readonly IProductService _productService;
    private readonly IShipmentService _shipmentService;

    public CustomerContextService(
        IWorkContext workContext,
        ICustomerService customerService,
        IOrderService orderService,
        IProductService productService,
        IShipmentService shipmentService)
    {
        _workContext = workContext;
        _customerService = customerService;
        _orderService = orderService;
        _productService = productService;
        _shipmentService = shipmentService;
    }

    public async Task<CustomerContext> GetCurrentCustomerContextAsync()
    {
        var customer = await _workContext.GetCurrentCustomerAsync();
        var isGuest = await _customerService.IsGuestAsync(customer);

        if (isGuest)
            return new CustomerContext { IsLoggedIn = false };

        var fullName = await _customerService.GetCustomerFullNameAsync(customer);
        var firstName = fullName?.Split(' ').FirstOrDefault() ?? "there";

        var orders = await _orderService.SearchOrdersAsync(
            customerId: customer.Id,
            pageSize: 5
        );

        var orderContexts = new List<OrderContext>();

        foreach (var order in orders.OrderByDescending(o => o.CreatedOnUtc))
        {
            var items = await _orderService.GetOrderItemsAsync(order.Id);
            var productNames = new List<string>();

            foreach (var item in items)
            {
                var product = await _productService.GetProductByIdAsync(item.ProductId);
                if (product != null)
                    productNames.Add(product.Name);
            }

            // Get shipment tracking if available
            var shipments = await _shipmentService.GetShipmentsByOrderIdAsync(order.Id);
            var trackingNumber = shipments.FirstOrDefault()?.TrackingNumber;

            orderContexts.Add(new OrderContext
            {
                OrderNumber = order.CustomOrderNumber,
                Status = order.OrderStatus.ToString(),
                OrderDate = order.CreatedOnUtc,
                OrderTotal = order.OrderTotal,
                TrackingNumber = trackingNumber,
                Products = productNames
            });
        }

        return new CustomerContext
        {
            IsLoggedIn = true,
            FirstName = firstName,
            RecentOrders = orderContexts
        };
    }
}
