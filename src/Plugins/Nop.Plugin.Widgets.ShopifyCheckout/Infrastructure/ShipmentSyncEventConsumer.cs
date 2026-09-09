using System;
using System.Linq;
using System.Threading.Tasks;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Shipping;
using Nop.Core.Events;
using Nop.Plugin.Widgets.ShopifyCheckout.Services;
using Nop.Services.Cms;
using Nop.Services.Common;
using Nop.Services.Events;
using Nop.Services.Logging;
using Nop.Services.Orders;

namespace Nop.Plugin.Widgets.ShopifyCheckout.Infrastructure;

/// <summary>
/// Listens to nopCommerce shipment lifecycle events (insert/update/sent)
/// and automatically creates fulfillments with tracking info in Shopify.
/// </summary>
public class ShipmentSyncEventConsumer :
    IConsumer<EntityInsertedEvent<Shipment>>,
    IConsumer<EntityUpdatedEvent<Shipment>>,
    IConsumer<ShipmentSentEvent>
{
    #region Fields

    private readonly IShopifyAdminApiService _adminApiService;
    private readonly IOrderService _orderService;
    private readonly IGenericAttributeService _genericAttributeService;
    private readonly IWidgetPluginManager _widgetPluginManager;
    private readonly ILogger _logger;
    private readonly ShopifyCheckoutSettings _settings;

    #endregion

    #region Ctor

    public ShipmentSyncEventConsumer(
        IShopifyAdminApiService adminApiService,
        IOrderService orderService,
        IGenericAttributeService genericAttributeService,
        IWidgetPluginManager widgetPluginManager,
        ILogger logger,
        ShopifyCheckoutSettings settings)
    {
        _adminApiService = adminApiService;
        _orderService = orderService;
        _genericAttributeService = genericAttributeService;
        _widgetPluginManager = widgetPluginManager;
        _logger = logger;
        _settings = settings;
    }

    #endregion

    #region Event Handlers

    public async Task HandleEventAsync(EntityInsertedEvent<Shipment> eventMessage)
    {
        if (eventMessage?.Entity != null)
            await SyncShipmentToShopifyAsync(eventMessage.Entity);
    }

    public async Task HandleEventAsync(EntityUpdatedEvent<Shipment> eventMessage)
    {
        if (eventMessage?.Entity != null)
            await SyncShipmentToShopifyAsync(eventMessage.Entity);
    }

    public async Task HandleEventAsync(ShipmentSentEvent eventMessage)
    {
        if (eventMessage?.Shipment != null)
            await SyncShipmentToShopifyAsync(eventMessage.Shipment);
    }

    #endregion

    #region Helpers

    private async Task SyncShipmentToShopifyAsync(Shipment shipment)
    {
        if (!await _widgetPluginManager.IsPluginActiveAsync(ShopifyCheckoutDefaults.SystemName))
            return;

        if (shipment == null || string.IsNullOrWhiteSpace(shipment.TrackingNumber))
            return;

        var order = await _orderService.GetOrderByIdAsync(shipment.OrderId);
        if (order == null)
            return;

        // Retrieve linked Shopify Order ID from GenericAttribute
        long shopifyOrderId = await _genericAttributeService.GetAttributeAsync<long>(order, "ShopifyOrderId");

        if (shopifyOrderId <= 0)
            return;

        var (success, message) = await _adminApiService.CreateFulfillmentAsync(
            shopifyOrderId,
            shipment.TrackingNumber.Trim(),
            trackingCompany: null,
            trackingUrl: null,
            notifyCustomer: true);

        if (success)
        {
            await _logger.InformationAsync($"ShipmentSyncEventConsumer: Successfully synced tracking number '{shipment.TrackingNumber}' for Shopify Order #{shopifyOrderId}.");
        }
        else
        {
            await _logger.WarningAsync($"ShipmentSyncEventConsumer: Failed to sync shipment to Shopify: {message}");
        }
    }

    #endregion
}
