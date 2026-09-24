using FluentMigrator.Builders.Create.Table;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.Widgets.AgentSearch.Domain;

namespace Nop.Plugin.Widgets.AgentSearch.Data
{
    public class AgentApiKeyBuilder : NopEntityBuilder<AgentApiKey>
    {
        public override void MapEntity(CreateTableExpressionBuilder table)
        {
            table
                .WithColumn(nameof(AgentApiKey.AgentName)).AsString(200).NotNullable()
                .WithColumn(nameof(AgentApiKey.KeyHash)).AsString(128).NotNullable()
                .WithColumn(nameof(AgentApiKey.KeyPrefix)).AsString(32).NotNullable()
                .WithColumn(nameof(AgentApiKey.IsActive)).AsBoolean().NotNullable()
                .WithColumn(nameof(AgentApiKey.RateLimitPerMinute)).AsInt32().NotNullable()
                .WithColumn(nameof(AgentApiKey.AllowedScopes)).AsString(500).NotNullable()
                .WithColumn(nameof(AgentApiKey.CreatedOnUtc)).AsDateTime2().NotNullable()
                .WithColumn(nameof(AgentApiKey.LastUsedOnUtc)).AsDateTime2().Nullable()
                .WithColumn(nameof(AgentApiKey.ExpiresOnUtc)).AsDateTime2().Nullable();
        }
    }
}
