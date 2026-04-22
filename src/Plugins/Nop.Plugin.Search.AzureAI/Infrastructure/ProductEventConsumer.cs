using Nop.Core.Domain.Catalog;
using Nop.Core.Events;
using Nop.Plugin.Search.AzureAI.Messages;
using Nop.Services.Catalog;
using Nop.Services.Events;

namespace Nop.Plugin.Search.AzureAI.Infrastructure;

/// <summary>
/// Listens to nopCommerce product lifecycle events and publishes
/// self-contained index messages to Azure Service Bus.
/// No API callback needed — all product data is included in the message.
/// </summary>
public class ProductEventConsumer :
    IConsumer<EntityInsertedEvent<Product>>,
    IConsumer<EntityUpdatedEvent<Product>>,
    IConsumer<EntityDeletedEvent<Product>>
{
    private readonly ServiceBusPublisher _publisher;
    private readonly AzureAISearchService _searchService;
    private readonly AzureAISearchSettings _settings;

    public ProductEventConsumer(
        ServiceBusPublisher publisher,
        AzureAISearchService searchService,
        AzureAISearchSettings settings)
    {
        _publisher = publisher;
        _searchService = searchService;
        _settings = settings;
    }

    public async Task HandleEventAsync(EntityInsertedEvent<Product> eventMessage)
    {
        if (!_settings.Enabled)
            return;

        await PublishIndexMessageAsync(eventMessage.Entity, ProductIndexAction.Index);
    }

    public async Task HandleEventAsync(EntityUpdatedEvent<Product> eventMessage)
    {
        if (!_settings.Enabled)
            return;

        // If product is being unpublished, remove from index rather than update
        var action = eventMessage.Entity.Published
            ? ProductIndexAction.Index
            : ProductIndexAction.Delete;

        await PublishIndexMessageAsync(eventMessage.Entity, action);
    }

    public async Task HandleEventAsync(EntityDeletedEvent<Product> eventMessage)
    {
        if (!_settings.Enabled)
            return;

        // For deletes we only need the ID — no product data required
        await _publisher.PublishAsync(new ProductIndexMessage
        {
            ProductId = eventMessage.Entity.Id,
            Action = ProductIndexAction.Delete
        });
    }

    private async Task PublishIndexMessageAsync(Product product, ProductIndexAction action)
    {
        var message = await _searchService.PrepareProductIndexMessageAsync(product, action);
        await _publisher.PublishAsync(message);
    }
}
