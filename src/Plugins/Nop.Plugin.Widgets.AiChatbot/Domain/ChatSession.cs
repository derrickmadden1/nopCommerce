using System;
using Nop.Core;

namespace Nop.Plugin.Widgets.AiChatbot.Domain;

/// <summary>
/// Represents a chatbot conversation session stored in the database.
/// </summary>
public class ChatSession : BaseEntity
{
    /// <summary>
    /// Unique session identifier generated for the browser/client
    /// </summary>
    public Guid SessionGuid { get; set; }

    /// <summary>
    /// Registered customer ID (0 for guest customers)
    /// </summary>
    public int CustomerId { get; set; }

    /// <summary>
    /// JSON serialized representation of List<ConversationTurn>
    /// </summary>
    public string MessagesJson { get; set; } = "[]";

    /// <summary>
    /// Session creation timestamp in UTC
    /// </summary>
    public DateTime CreatedOnUtc { get; set; }

    /// <summary>
    /// Timestamp in UTC when last message was added
    /// </summary>
    public DateTime UpdatedOnUtc { get; set; }
}
