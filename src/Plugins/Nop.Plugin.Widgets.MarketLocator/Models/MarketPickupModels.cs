using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Web.Framework.Models;

namespace Nop.Plugin.Widgets.MarketLocator.Models;

/// <summary>
/// Drives the location + date picker injected into the checkout shipping step.
/// </summary>
public class MarketPickupSelectorModel : BaseNopModel
{
    /// <summary>All published markets that have at least one upcoming date.</summary>
    public List<MarketPickupOption> Markets { get; set; } = new();

    /// <summary>Market Id pre-selected from the customer's session (if returning to step).</summary>
    public int SelectedMarketId { get; set; }

    /// <summary>Date string pre-selected from the customer's session.</summary>
    public string SelectedDate { get; set; } = string.Empty;

    /// <summary>True when "Market Pickup" is the currently chosen shipping method.</summary>
    public bool IsMarketPickupSelected { get; set; }
}

public class MarketPickupOption
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Hours { get; set; } = string.Empty;

    /// <summary>Upcoming dates as SelectListItems for the date dropdown.</summary>
    public List<SelectListItem> DateOptions { get; set; } = new();
}

/// <summary>
/// Posted from the checkout widget when the customer confirms their selection.
/// </summary>
public class MarketPickupSelectionModel
{
    public int MarketId { get; set; }
    public string PickupDate { get; set; } = string.Empty;
}

/// <summary>
/// Shown in the order detail sidebar / confirmation email.
/// </summary>
public class MarketPickupSummaryModel : BaseNopModel
{
    public bool HasPickup { get; set; }
    public string MarketName { get; set; } = string.Empty;
    public string PickupDate { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Hours { get; set; } = string.Empty;
    public string DirectionsUrl { get; set; } = string.Empty;
}
