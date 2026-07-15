namespace Nop.Plugin.Widgets.AiChatbot.Models;

public class ChatRequest
{
    public string Message { get; set; } = string.Empty;
    public List<ConversationTurn> History { get; set; } = new();
}

public class ChatResponse
{
    public string Response { get; set; } = string.Empty;
    public bool Success { get; set; } = true;
    public string? Error { get; set; }
}

public class ConversationTurn
{
    public string Role { get; set; } = string.Empty; // "user" or "assistant"
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// Customer context injected into the system prompt
/// </summary>
public class CustomerContext
{
    public bool IsLoggedIn { get; set; }
    public string? FirstName { get; set; }
    public List<OrderContext> RecentOrders { get; set; } = new();
}

public class OrderContext
{
    public string OrderNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public decimal OrderTotal { get; set; }
    public string? TrackingNumber { get; set; }
    public List<string> Products { get; set; } = new();
}
