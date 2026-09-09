using FluentMigrator.Builders.Create.Table;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.Widgets.AiChatbot.Domain;

namespace Nop.Plugin.Widgets.AiChatbot.Data;

public class ChatSessionBuilder : NopEntityBuilder<ChatSession>
{
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        table
            .WithColumn(nameof(ChatSession.SessionGuid)).AsGuid().NotNullable()
            .WithColumn(nameof(ChatSession.CustomerId)).AsInt32().NotNullable()
            .WithColumn(nameof(ChatSession.MessagesJson)).AsString(int.MaxValue).NotNullable()
            .WithColumn(nameof(ChatSession.CreatedOnUtc)).AsDateTime2().NotNullable()
            .WithColumn(nameof(ChatSession.UpdatedOnUtc)).AsDateTime2().NotNullable();
    }
}
