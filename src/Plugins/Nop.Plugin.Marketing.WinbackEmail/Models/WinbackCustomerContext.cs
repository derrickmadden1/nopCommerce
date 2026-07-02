namespace Nop.Plugin.Marketing.WinbackEmail.Models;

/// <summary>
/// Everything Azure OpenAI needs to generate a personalised winback email
/// </summary>
public class WinbackCustomerContext
{
    public string CustomerFirstName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public int EmailNumber { get; set; } // 1, 2, or 3
    public int DaysSinceLastOrder { get; set; }
    public List<OrderSummary> RecentOrders { get; set; } = new();
    public string? DiscountCode { get; set; }
    public string StoreName { get; set; } = string.Empty;
}

public class OrderSummary
{
    public DateTime OrderDate { get; set; }
    public decimal OrderTotal { get; set; }
    public List<string> ProductNames { get; set; } = new();
}

public class GeneratedEmail
{
    public string Subject { get; set; } = string.Empty;
    public string HtmlBody { get; set; } = string.Empty;
}
