using Nop.Core;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Orders;
using Nop.Plugin.Widgets.MarketLocator.Models;
using Nop.Services.Common;

namespace Nop.Plugin.Widgets.MarketLocator.Services;

public interface IMarketPickupService
{
    /// <summary>Persists the customer's in-progress checkout selection.</summary>
    Task SaveSelectionAsync(Customer customer, int marketId, string pickupDate);

    /// <summary>Reads the customer's in-progress selection.</summary>
    Task<(int marketId, string pickupDate)> GetSelectionAsync(Customer customer);

    /// <summary>Clears the temporary checkout selection (called after order placed).</summary>
    Task ClearSelectionAsync(Customer customer);

    /// <summary>
    /// Stamps the final pickup details onto the completed order as GenericAttributes.
    /// Call this from an OrderPlacedEventConsumer.
    /// </summary>
    Task StampOrderAsync(Order order);

    /// <summary>Reads stamped pickup details from a completed order.</summary>
    Task<MarketPickupSummaryModel> GetOrderSummaryAsync(Order order);
}

public class MarketPickupService : IMarketPickupService
{
    private readonly IGenericAttributeService _genericAttributeService;
    private readonly IMarketLocationService _locationService;

    public MarketPickupService(
        IGenericAttributeService genericAttributeService,
        IMarketLocationService locationService)
    {
        _genericAttributeService = genericAttributeService;
        _locationService = locationService;
    }

    // ── Customer session ──────────────────────────────────────────────────────

    public async Task SaveSelectionAsync(Customer customer, int marketId, string pickupDate)
    {
        await _genericAttributeService.SaveAttributeAsync(
            customer, MarketPickupAttributeNames.CustomerSelectedMarketId, marketId);
        await _genericAttributeService.SaveAttributeAsync(
            customer, MarketPickupAttributeNames.CustomerSelectedMarketDate, pickupDate);
    }

    public async Task<(int marketId, string pickupDate)> GetSelectionAsync(Customer customer)
    {
        var marketId = await _genericAttributeService.GetAttributeAsync<int>(
            customer, MarketPickupAttributeNames.CustomerSelectedMarketId);
        var date = await _genericAttributeService.GetAttributeAsync<string>(
            customer, MarketPickupAttributeNames.CustomerSelectedMarketDate) ?? string.Empty;
        return (marketId, date);
    }

    public async Task ClearSelectionAsync(Customer customer)
    {
        await _genericAttributeService.SaveAttributeAsync<int>(
            customer, MarketPickupAttributeNames.CustomerSelectedMarketId, 0);
        await _genericAttributeService.SaveAttributeAsync<string>(
            customer, MarketPickupAttributeNames.CustomerSelectedMarketDate, string.Empty);
    }

    // ── Order stamping ────────────────────────────────────────────────────────

    public async Task StampOrderAsync(Order order)
    {
        // Read the selection we stored on the customer during checkout.
        var customer = new Nop.Core.Domain.Customers.Customer { Id = order.CustomerId };

        var marketId = await _genericAttributeService.GetAttributeAsync<int>(
            customer, MarketPickupAttributeNames.CustomerSelectedMarketId);
        var pickupDate = await _genericAttributeService.GetAttributeAsync<string>(
            customer, MarketPickupAttributeNames.CustomerSelectedMarketDate) ?? string.Empty;

        if (marketId <= 0 || string.IsNullOrWhiteSpace(pickupDate))
            return; // Not a market pickup order

        var market = await _locationService.GetByIdAsync(marketId);
        if (market is null) return;

        // Snapshot the market details onto the order so they survive future edits.
        await _genericAttributeService.SaveAttributeAsync(
            order, MarketPickupAttributeNames.OrderPickupMarketId, marketId);
        await _genericAttributeService.SaveAttributeAsync(
            order, MarketPickupAttributeNames.OrderPickupMarketName, market.Name);
        await _genericAttributeService.SaveAttributeAsync(
            order, MarketPickupAttributeNames.OrderPickupDate, pickupDate);
        await _genericAttributeService.SaveAttributeAsync(
            order, MarketPickupAttributeNames.OrderPickupAddress, market.Address);
        await _genericAttributeService.SaveAttributeAsync(
            order, MarketPickupAttributeNames.OrderPickupHours, market.Hours);
    }

    // ── Order summary read ────────────────────────────────────────────────────

    public async Task<MarketPickupSummaryModel> GetOrderSummaryAsync(Order order)
    {
        var marketId = await _genericAttributeService.GetAttributeAsync<int>(
            order, MarketPickupAttributeNames.OrderPickupMarketId);

        if (marketId <= 0)
            return new MarketPickupSummaryModel { HasPickup = false };

        var name    = await _genericAttributeService.GetAttributeAsync<string>(order, MarketPickupAttributeNames.OrderPickupMarketName) ?? string.Empty;
        var date    = await _genericAttributeService.GetAttributeAsync<string>(order, MarketPickupAttributeNames.OrderPickupDate) ?? string.Empty;
        var address = await _genericAttributeService.GetAttributeAsync<string>(order, MarketPickupAttributeNames.OrderPickupAddress) ?? string.Empty;
        var hours   = await _genericAttributeService.GetAttributeAsync<string>(order, MarketPickupAttributeNames.OrderPickupHours) ?? string.Empty;

        return new MarketPickupSummaryModel
        {
            HasPickup    = true,
            MarketName   = name,
            PickupDate   = date,
            Address      = address,
            Hours        = hours,
            DirectionsUrl = $"https://www.google.com/maps/search/?api=1&query={Uri.EscapeDataString(address)}",
        };
    }
}
