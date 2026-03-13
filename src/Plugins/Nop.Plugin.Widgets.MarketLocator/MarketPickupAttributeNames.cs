namespace Nop.Plugin.Widgets.MarketLocator;

/// <summary>
/// Centralised key names for GenericAttribute storage on orders and customers.
/// Using constants avoids magic strings scattered across the codebase.
/// </summary>
public static class MarketPickupAttributeNames
{
    // ── Stored on the Order entity ────────────────────────────────────────────

    /// <summary>int — Id of the chosen MarketLocation.</summary>
    public const string OrderPickupMarketId   = "MarketLocator.PickupMarketId";

    /// <summary>string — Display name snapshot, e.g. "Downtown Farmers Market".</summary>
    public const string OrderPickupMarketName = "MarketLocator.PickupMarketName";

    /// <summary>string — Chosen date string, e.g. "Sat, Mar 21".</summary>
    public const string OrderPickupDate       = "MarketLocator.PickupDate";

    /// <summary>string — Address snapshot at time of order.</summary>
    public const string OrderPickupAddress    = "MarketLocator.PickupAddress";

    /// <summary>string — Hours snapshot, e.g. "8:00 AM – 1:00 PM".</summary>
    public const string OrderPickupHours      = "MarketLocator.PickupHours";

    // ── Stored on the Customer entity (cleared after order placed) ────────────

    /// <summary>Temporary selection held while the customer is in checkout.</summary>
    public const string CustomerSelectedMarketId   = "MarketLocator.Checkout.MarketId";
    public const string CustomerSelectedMarketDate = "MarketLocator.Checkout.MarketDate";
}
