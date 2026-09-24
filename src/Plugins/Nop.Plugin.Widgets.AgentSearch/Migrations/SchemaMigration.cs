using FluentMigrator;
using Nop.Data.Extensions;
using Nop.Data.Migrations;
using Nop.Plugin.Widgets.AgentSearch.Domain;

namespace Nop.Plugin.Widgets.AgentSearch.Migrations
{
    [NopMigration("2026-09-24 13:00:00", "AgentApiKey table creation", MigrationProcessType.Update)]
    public class SchemaMigration : Migration
    {
        public override void Up()
        {
            this.CreateTableIfNotExists<AgentApiKey>();

            Create.Index("IX_AgentApiKey_KeyHash")
                .OnTable(nameof(AgentApiKey))
                .OnColumn(nameof(AgentApiKey.KeyHash)).Ascending()
                .WithOptions().Unique();

            Create.Index("IX_AgentApiKey_IsActive_Expires")
                .OnTable(nameof(AgentApiKey))
                .OnColumn(nameof(AgentApiKey.IsActive)).Ascending()
                .OnColumn(nameof(AgentApiKey.ExpiresOnUtc)).Ascending();
        }

        public override void Down()
        {
            // Table persistent across re-installs unless explicitly dropped
        }
    }
}
