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

    /// <summary>
    /// Optional action for the frontend to execute after showing the message
    /// </summary>
    public ChatAction? Action { get; set; }
}
public class ChatAction
{
    /// <summary>
    /// addToCart | navigate | viewCart | checkout
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// For navigate actions — the URL to go to
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// For addToCart actions — nopCommerce product ID
    /// </summary>
    public int? ProductId { get; set; }

    /// <summary>
    /// For addToCart actions — quantity to add (default 1)
    /// </summary>
    public int Quantity { get; set; } = 1;

    /// <summary>
    /// Human-readable label for the action e.g. product name
    /// </summary>
    public string? Label { get; set; }
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
    public ShoppingCartContext? Cart { get; set; }
}

public class ShoppingCartContext
{
    public List<ShoppingCartItemContext> Items { get; set; } = new();
    public decimal Total { get; set; }
}

public class ShoppingCartItemContext
{
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal SubTotal { get; set; }
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

/// <summary>
/// Structured response from Azure OpenAI — parsed from JSON
/// </summary>
public class AiStructuredResponse
{
    public string Message { get; set; } = string.Empty;
    public AiAction? Action { get; set; }
}

public class AiAction
{
    public string Type { get; set; } = string.Empty;
    public string? Url { get; set; }
    public int? ProductId { get; set; }
    public int Quantity { get; set; } = 1;
    public string? Label { get; set; }
}
