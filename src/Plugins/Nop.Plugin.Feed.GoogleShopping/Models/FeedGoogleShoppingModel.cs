using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.Feed.GoogleShopping.Models;

public record FeedGoogleShoppingModel
{
    public FeedGoogleShoppingModel()
    {
        AvailableCurrencies = new List<SelectListItem>();
        AvailableGoogleCategories = new List<SelectListItem>();
        GeneratedFiles = new List<GeneratedFileModel>();
        GoogleProductSearchModel = new GoogleProductSearchModel();
    }

    public int ActiveStoreScopeConfiguration { get; set; }

    [NopResourceDisplayName("Plugins.Feed.GoogleShopping.ProductPictureSize")]
    public int ProductPictureSize { get; set; }
    public bool ProductPictureSize_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.Feed.GoogleShopping.Currency")]
    public int CurrencyId { get; set; }
    public IList<SelectListItem> AvailableCurrencies { get; set; }
    public bool CurrencyId_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.Feed.GoogleShopping.DefaultGoogleCategory")]
    public string DefaultGoogleCategory { get; set; }
    public IList<SelectListItem> AvailableGoogleCategories { get; set; }
    public bool DefaultGoogleCategory_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.Feed.GoogleShopping.PassShippingInfoWeight")]
    public bool PassShippingInfoWeight { get; set; }
    public bool PassShippingInfoWeight_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.Feed.GoogleShopping.PassShippingInfoDimensions")]
    public bool PassShippingInfoDimensions { get; set; }
    public bool PassShippingInfoDimensions_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.Feed.GoogleShopping.PricesConsiderPromotions")]
    public bool PricesConsiderPromotions { get; set; }
    public bool PricesConsiderPromotions_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.Feed.GoogleShopping.UseAzureBlobStorage")]
    public bool UseAzureBlobStorage { get; set; }
    public bool UseAzureBlobStorage_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.Feed.GoogleShopping.AzureBlobConnectionString")]
    public string AzureBlobConnectionString { get; set; } = string.Empty;
    public bool AzureBlobConnectionString_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.Feed.GoogleShopping.AzureBlobContainerName")]
    public string AzureBlobContainerName { get; set; } = string.Empty;
    public bool AzureBlobContainerName_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.Feed.GoogleShopping.AzureBlobEndPoint")]
    public string AzureBlobEndPoint { get; set; } = string.Empty;
    public bool AzureBlobEndPoint_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.Feed.GoogleShopping.AzureBlobAppendContainerName")]
    public bool AzureBlobAppendContainerName { get; set; }
    public bool AzureBlobAppendContainerName_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.Feed.GoogleShopping.PassCheckoutLinkTemplate")]
    public bool PassCheckoutLinkTemplate { get; set; }
    public bool PassCheckoutLinkTemplate_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.Feed.GoogleShopping.StaticFilePath")]
    public IList<GeneratedFileModel> GeneratedFiles { get; set; }

    public bool HideGeneralBlock { get; set; }

    public bool HideProductSettingsBlock { get; set; }

    public GoogleProductSearchModel GoogleProductSearchModel { get; set; }
}