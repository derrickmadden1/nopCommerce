using FluentMigrator;
using Nop.Core.Domain.Messages;

namespace Nop.Data.Migrations.UpgradeTo500;

[NopUpdateMigration("2026-04-20 00:00:00", "5.00", UpdateMigrationType.Data)]
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
        //#309
        if (!_dataProvider.GetTable<MessageTemplate>().Any(st => string.Compare(st.Name, MessageTemplateSystemNames.NEXT_RECURRING_PAYMENT_CUSTOMER_NOTIFICATION, StringComparison.InvariantCultureIgnoreCase) == 0))
        {
            var eaGeneral = _dataProvider.GetTable<EmailAccount>().FirstOrDefault() ?? throw new Exception("Default email account cannot be loaded");
            _dataProvider.InsertEntity(new MessageTemplate
            {
                Name = MessageTemplateSystemNames.NEXT_RECURRING_PAYMENT_CUSTOMER_NOTIFICATION,
                Subject = "%Store.Name%. Notification of upcoming payment",
                Body = $"<p>{Environment.NewLine}<a href=\"%Store.URL%\">%Store.Name%</a>{Environment.NewLine}<br />{Environment.NewLine}<br />{Environment.NewLine}Hello %Customer.FullName%,{Environment.NewLine}<br />{Environment.NewLine}The next payment for order <a href=\"%Order.OrderURLForCustomer%\" target=\"_blank\">%Order.OrderNumber%</a> will be debited in %RecurringPayment.NextRecurringPaymentDelay% day(s).{Environment.NewLine}<br />{Environment.NewLine}Please make sure you have sufficient funds on your card for the upcoming debit.</p>{Environment.NewLine}",
                IsActive = true,
                EmailAccountId = eaGeneral.Id
            });
        }
    }

    /// <summary>Collects the DOWN migration expressions</summary>
    public override void Down()
    {
        //add the downgrade logic if necessary 
    }
}