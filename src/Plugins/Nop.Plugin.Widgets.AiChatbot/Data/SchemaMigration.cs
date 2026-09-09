using FluentMigrator;
using Nop.Data.Extensions;
using Nop.Data.Migrations;
using Nop.Plugin.Widgets.AiChatbot.Domain;

namespace Nop.Plugin.Widgets.AiChatbot.Data;

[NopSchemaMigration("2026-08-05 00:00:00", "Widgets.AiChatbot base schema")]
public class SchemaMigration : AutoReversingMigration
{
    public override void Up()
    {
        this.CreateTableIfNotExists<ChatSession>();
    }
}
