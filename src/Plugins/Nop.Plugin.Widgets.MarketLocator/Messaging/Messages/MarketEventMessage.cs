namespace Nop.Plugin.Widgets.MarketLocator.Messaging.Messages
{
    public class MarketEventMessage
    {
        public string ChangeType { get; set; } = string.Empty;   // "Created" | "Updated"
        public string MarketName { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? Description { get; set; }
        public string MapUrl { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
    }
}
