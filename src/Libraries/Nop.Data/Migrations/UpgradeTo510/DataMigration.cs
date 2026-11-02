using System.Text.RegularExpressions;
using FluentMigrator;
using Nop.Core.Domain.Messages;

namespace Nop.Data.Migrations.UpgradeTo510;

[NopUpdateMigration("2026-11-02 15:00:00", "5.10", UpdateMigrationType.Data)]
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
        var trackingLine = $"%if (!String.IsNullOrEmpty(%Shipment.TrackingNumber%))Tracking number: %if (!String.IsNullOrEmpty(%Shipment.TrackingNumberURL%))<a href=\"%Shipment.TrackingNumberURL%\" target=\"_blank\">%Shipment.TrackingNumber%</a>endif%%if (String.IsNullOrEmpty(%Shipment.TrackingNumberURL%))%Shipment.TrackingNumber%endif%{Environment.NewLine}<br />{Environment.NewLine}%if (!String.IsNullOrEmpty(%Shipment.TrackingNumberURL%))Tracking URL: <a href=\"%Shipment.TrackingNumberURL%\" target=\"_blank\">%Shipment.TrackingNumberURL%</a>{Environment.NewLine}<br />{Environment.NewLine}endif%endif%<br />";
        var shippingMethodRegex = new Regex(@"Shipping Method: %Order\.ShippingMethod%\s*<br\s*/?>\s*<br\s*/?>", RegexOptions.IgnoreCase);
        var oldTrackingRegex = new Regex(@"%if \(!String\.IsNullOrEmpty\(%Shipment\.TrackingNumber%\)\).*?endif%<br\s*/?>", RegexOptions.IgnoreCase | RegexOptions.Singleline);

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
            else if (!shipmentSentTemplate.Body.Contains("Tracking URL:"))
            {
                if (oldTrackingRegex.IsMatch(shipmentSentTemplate.Body))
                {
                    shipmentSentTemplate.Body = oldTrackingRegex.Replace(shipmentSentTemplate.Body, trackingLine);
                    _dataProvider.UpdateEntity(shipmentSentTemplate);
                }
            }
        }
    }

    /// <summary>Collects the DOWN migration expressions</summary>
    public override void Down()
    {
        //add the downgrade logic if necessary 
    }
}
