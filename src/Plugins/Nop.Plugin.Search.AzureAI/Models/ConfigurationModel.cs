using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.Search.AzureAI.Models;

public record ConfigurationModel : BaseNopModel
{
    [NopResourceDisplayName("Plugins.Search.AzureAI.Enabled")]
    public bool Enabled { get; set; }

    [NopResourceDisplayName("Plugins.Search.AzureAI.Endpoint")]
    public string Endpoint { get; set; } = string.Empty;

    [NopResourceDisplayName("Plugins.Search.AzureAI.QueryApiKey")]
    public string QueryApiKey { get; set; } = string.Empty;

    [NopResourceDisplayName("Plugins.Search.AzureAI.IndexName")]
    public string IndexName { get; set; } = "products";

    [NopResourceDisplayName("Plugins.Search.AzureAI.ServiceBusConnectionString")]
    public string ServiceBusConnectionString { get; set; } = string.Empty;

    [NopResourceDisplayName("Plugins.Search.AzureAI.ServiceBusQueueName")]
    public string ServiceBusQueueName { get; set; } = "product-index";

    [NopResourceDisplayName("Plugins.Search.AzureAI.PageSize")]
    public int PageSize { get; set; } = 20;
}
