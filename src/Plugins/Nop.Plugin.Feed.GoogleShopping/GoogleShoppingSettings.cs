using Nop.Core.Configuration;

namespace Nop.Plugin.Feed.GoogleShopping;

public class GoogleShoppingSettings : ISettings
{
    /// <summary>
    /// Product picture size
    /// </summary>
    public int ProductPictureSize { get; set; }

    /// <summary>
    /// A value indicating whether we should pass shipping info (weight)
    /// </summary>
    public bool PassShippingInfoWeight { get; set; }

    /// <summary>
    /// A value indicating whether we should pass shipping info (dimensions)
    /// </summary>
    public bool PassShippingInfoDimensions { get; set; }

    /// <summary>
    /// A value indicating whether we should calculate prices considering promotions (tier prices, discounts, special prices, etc)
    /// </summary>
    public bool PricesConsiderPromotions { get; set; }

    /// <summary>
    /// Currency identifier for which feed file(s) will be generated
    /// </summary>
    public int CurrencyId { get; set; }

    /// <summary>
    /// Default Google category
    /// </summary>
    public string DefaultGoogleCategory { get; set; }

    /// <summary>
    /// Static file name of the feed
    /// </summary>
    public string StaticFileName { get; set; }

    /// <summary>
    /// Number of days for expiration date
    /// </summary>
    public int ExpirationNumberOfDays { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether we should use Azure Blob Storage for storing the feed
    /// </summary>
    public bool UseAzureBlobStorage { get; set; }

    /// <summary>
    /// Gets or sets the Azure Blob Storage connection string
    /// </summary>
    public string AzureBlobConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Azure Blob Storage container name
    /// </summary>
    public string AzureBlobContainerName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Azure Blob Storage endpoint
    /// </summary>
    public string AzureBlobEndPoint { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the container name should be appended to the endpoint
    /// </summary>
    public bool AzureBlobAppendContainerName { get; set; }
}