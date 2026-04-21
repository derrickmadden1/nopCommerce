namespace Nop.Plugin.Widgets.MarketLocator.Messaging.Messages
{
    public class MarketEventMessage
    {
        public string ChangeType { get; set; }   // "Created" | "Updated"
        public string MarketName { get; set; }
        public string Location { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? Description { get; set; }
        public string MapUrl { get; set; }
    }
}
