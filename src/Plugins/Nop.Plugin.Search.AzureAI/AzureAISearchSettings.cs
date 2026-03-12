using Nop.Core.Configuration;

namespace Nop.Plugin.Search.AzureAI;

public class AzureAISearchSettings : ISettings
{
    /// <summary>
    /// Azure AI Search service endpoint e.g. https://yourservice.search.windows.net
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// Query API key (read-only) — do not use the Admin key here
    /// </summary>
    public string QueryApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Name of the products index in Azure AI Search
    /// </summary>
    public string IndexName { get; set; } = "products";

    /// <summary>
    /// Number of results to request from Azure AI Search per page
    /// </summary>
    public int PageSize { get; set; } = 20;

    /// <summary>
    /// Whether the plugin is active and should handle search requests
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Azure Service Bus connection string (send-only policy recommended)
    /// </summary>
    public string ServiceBusConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Name of the Service Bus queue to publish product index messages to
    /// </summary>
    public string ServiceBusQueueName { get; set; } = "product-index";
}
