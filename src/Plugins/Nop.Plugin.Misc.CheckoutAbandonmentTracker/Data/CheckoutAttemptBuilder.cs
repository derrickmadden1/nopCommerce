using FluentMigrator.Builders.Create.Table;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.Misc.CheckoutAbandonmentTracker.Domain;

namespace Nop.Plugin.Misc.CheckoutAbandonmentTracker.Data
{
    public class CheckoutAttemptBuilder : NopEntityBuilder<CheckoutAttempt>
    {
        public override void MapEntity(CreateTableExpressionBuilder table)
        {
            table
                .WithColumn(nameof(CheckoutAttempt.CustomerId)).AsInt32().Nullable()
                .WithColumn(nameof(CheckoutAttempt.CustomerGuid)).AsString(36).NotNullable()
                .WithColumn(nameof(CheckoutAttempt.StoreId)).AsInt32().NotNullable()
                .WithColumn(nameof(CheckoutAttempt.StartedOnUtc)).AsDateTime2().NotNullable()
                .WithColumn(nameof(CheckoutAttempt.LastActivityUtc)).AsDateTime2().NotNullable()
                .WithColumn(nameof(CheckoutAttempt.LastStepReachedId)).AsInt32().NotNullable()
                .WithColumn(nameof(CheckoutAttempt.OrderId)).AsInt32().Nullable()
                .WithColumn(nameof(CheckoutAttempt.IsAbandoned)).AsBoolean().NotNullable()
                .WithColumn(nameof(CheckoutAttempt.CartTotal)).AsDecimal(18, 4).Nullable();
        }
    }
}
