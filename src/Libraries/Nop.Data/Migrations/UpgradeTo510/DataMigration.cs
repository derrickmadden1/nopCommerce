using Nop.Core.Domain.Localization;
using System.Text.RegularExpressions;
using FluentMigrator;
using Nop.Core.Domain.Messages;

namespace Nop.Data.Migrations.UpgradeTo510;

[NopUpdateMigration("2026-11-02 15:30:00", "5.10", UpdateMigrationType.Data)]
public class DataMigration : Migration
{
    private readonly INopDataProvider _dataProvider;

    public DataMigration(INopDataProvider dataProvider)
    {
        _dataProvider = dataProvider;
    }

    /// <summary>
    /// Collect the UP migration expressions
    /// </summary>
    public override void Up()
    {
        var trackingLine = $"%if (!String.IsNullOrEmpty(%Shipment.TrackingNumber%))Tracking number: %if (!String.IsNullOrEmpty(%Shipment.TrackingNumberURL%))<a href=\"%Shipment.TrackingNumberURL%\" target=\"_blank\">%Shipment.TrackingNumber%</a>endif%%if (String.IsNullOrEmpty(%Shipment.TrackingNumberURL%))%Shipment.TrackingNumber%endif%{Environment.NewLine}<br />{Environment.NewLine}endif%%if (!String.IsNullOrEmpty(%Shipment.TrackingNumberURL%))Tracking URL: <a href=\"%Shipment.TrackingNumberURL%\" target=\"_blank\">%Shipment.TrackingNumberURL%</a>{Environment.NewLine}<br />{Environment.NewLine}endif%<br />";
        var shippingMethodRegex = new Regex(@"Shipping Method: %Order\.ShippingMethod%\s*<br\s*/?>\s*<br\s*/?>", RegexOptions.IgnoreCase);
        var oldTrackingRegex = new Regex(@"%if \(!String\.IsNullOrEmpty\(%Shipment\.TrackingNumber%\)\).*?endif%(?:endif%)?<br\s*/?>", RegexOptions.IgnoreCase | RegexOptions.Singleline);

        var shipmentSentTemplates = _dataProvider.GetTable<MessageTemplate>()
            .Where(st => st.Name == MessageTemplateSystemNames.SHIPMENT_SENT_CUSTOMER_NOTIFICATION)
            .ToList();

        foreach (var shipmentSentTemplate in shipmentSentTemplates)
        {
            if (string.IsNullOrEmpty(shipmentSentTemplate.Body))
                continue;

            if (!shipmentSentTemplate.Body.Contains("%Shipment.TrackingNumber%"))
            {
                if (shippingMethodRegex.IsMatch(shipmentSentTemplate.Body))
                {
                    shipmentSentTemplate.Body = shippingMethodRegex.Replace(
                        shipmentSentTemplate.Body,
                        $"Shipping Method: %Order.ShippingMethod%{Environment.NewLine}<br />{Environment.NewLine}{trackingLine}");
                    _dataProvider.UpdateEntity(shipmentSentTemplate);
                }
            }
            else if (!shipmentSentTemplate.Body.Contains("endif%%if (!String.IsNullOrEmpty(%Shipment.TrackingNumberURL%))Tracking URL:"))
            {
                if (oldTrackingRegex.IsMatch(shipmentSentTemplate.Body))
                {
                    shipmentSentTemplate.Body = oldTrackingRegex.Replace(shipmentSentTemplate.Body, trackingLine);
                    _dataProvider.UpdateEntity(shipmentSentTemplate);
                }
            }
        }
        var languages = _dataProvider.GetTable<Language>().ToList();
        var lsrTable = _dataProvider.GetTable<LocaleStringResource>();
        foreach (var lang in languages)
        {
            if (!lsrTable.Any(r => r.LanguageId == lang.Id && r.ResourceName == "Admin.Catalog.Products.Fields.FixedPriceBundle"))
            {
                _dataProvider.InsertEntity(new LocaleStringResource
                {
                    LanguageId = lang.Id,
                    ResourceName = "Admin.Catalog.Products.Fields.FixedPriceBundle",
                    ResourceValue = "Fixed price bundle"
                });
            }
            if (!lsrTable.Any(r => r.LanguageId == lang.Id && r.ResourceName == "Admin.Catalog.Products.Fields.FixedPriceBundle.Hint"))
            {
                _dataProvider.InsertEntity(new LocaleStringResource
                {
                    LanguageId = lang.Id,
                    ResourceName = "Admin.Catalog.Products.Fields.FixedPriceBundle.Hint",
                    ResourceValue = "Check if this product is a fixed price bundle. When enabled, associated product prices will not be added to this bundle; the product uses its own base price plus any manual attribute price adjustments, while still tracking stock on associated products."
                });
            }
            if (!lsrTable.Any(r => r.LanguageId == lang.Id && r.ResourceName == "Admin.Catalog.Products.Fields.DynamicDualPreview"))
            {
                _dataProvider.InsertEntity(new LocaleStringResource
                {
                    LanguageId = lang.Id,
                    ResourceName = "Admin.Catalog.Products.Fields.DynamicDualPreview",
                    ResourceValue = "Dynamic dual preview"
                });
            }
            if (!lsrTable.Any(r => r.LanguageId == lang.Id && r.ResourceName == "Admin.Catalog.Products.Fields.DynamicDualPreview.Hint"))
            {
                _dataProvider.InsertEntity(new LocaleStringResource
                {
                    LanguageId = lang.Id,
                    ResourceName = "Admin.Catalog.Products.Fields.DynamicDualPreview.Hint",
                    ResourceValue = "Check to dynamically display a side-by-side preview of the selected bundle attributes (e.g. soap and candle together) in the main product image gallery. If unchecked, the product defaults to standard nopCommerce attribute combinations."
                });
            }
        }
    }

    /// <summary>Collects the DOWN migration expressions</summary>
    public override void Down()
    {
        //add the downgrade logic if necessary 
    }
}
