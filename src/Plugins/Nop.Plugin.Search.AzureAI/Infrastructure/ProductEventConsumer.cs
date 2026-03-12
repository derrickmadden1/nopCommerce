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
    private readonly AzureAISearchSettings _settings;
    private readonly ICategoryService _categoryService;
    private readonly IManufacturerService _manufacturerService;

    public ProductEventConsumer(
        ServiceBusPublisher publisher,
        AzureAISearchSettings settings,
        ICategoryService categoryService,
        IManufacturerService manufacturerService)
    {
        _publisher = publisher;
        _settings = settings;
        _categoryService = categoryService;
        _manufacturerService = manufacturerService;
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
        // Load category names via nopCommerce services
        var productCategories = await _categoryService.GetProductCategoriesByProductIdAsync(product.Id);
        var categoryNames = new List<string>();
        foreach (var pc in productCategories)
        {
            var category = await _categoryService.GetCategoryByIdAsync(pc.CategoryId);
            if (category != null && !category.Deleted)
                categoryNames.Add(category.Name);
        }

        // Load manufacturer names via nopCommerce services
        var productManufacturers = await _manufacturerService.GetProductManufacturersByProductIdAsync(product.Id);
        var manufacturerNames = new List<string>();
        foreach (var pm in productManufacturers)
        {
            var manufacturer = await _manufacturerService.GetManufacturerByIdAsync(pm.ManufacturerId);
            if (manufacturer != null && !manufacturer.Deleted)
                manufacturerNames.Add(manufacturer.Name);
        }

        await _publisher.PublishAsync(new ProductIndexMessage
        {
            ProductId = product.Id,
            Action = action,
            Name = product.Name,
            ShortDescription = product.ShortDescription,
            FullDescription = product.FullDescription,
            Sku = product.Sku,
            Price = product.Price,
            Published = product.Published,
            CategoryNames = categoryNames,
            ManufacturerNames = manufacturerNames
        });
    }
}
