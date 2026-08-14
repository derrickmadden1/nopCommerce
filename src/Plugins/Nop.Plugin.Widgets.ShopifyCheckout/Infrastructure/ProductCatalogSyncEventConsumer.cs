using System.Threading.Tasks;
using Nop.Core.Domain.Catalog;
using Nop.Core.Events;
using Nop.Plugin.Widgets.ShopifyCheckout.Services;
using Nop.Services.Catalog;
using Nop.Services.Events;

namespace Nop.Plugin.Widgets.ShopifyCheckout.Infrastructure;

/// <summary>
/// Listens to nopCommerce product & attribute combination lifecycle events (create/update/delete)
/// and automatically pushes changes to Shopify Admin API, mapping the resulting Variant GID locally.
/// </summary>
public class ProductCatalogSyncEventConsumer :
    IConsumer<EntityInsertedEvent<Product>>,
    IConsumer<EntityUpdatedEvent<Product>>,
    IConsumer<EntityDeletedEvent<Product>>,
    IConsumer<EntityInsertedEvent<ProductAttributeCombination>>,
    IConsumer<EntityUpdatedEvent<ProductAttributeCombination>>,
    IConsumer<EntityDeletedEvent<ProductAttributeCombination>>
{
    #region Fields

    private readonly IShopifyAdminApiService _adminApiService;
    private readonly IProductService _productService;
    private readonly ShopifyCheckoutSettings _settings;

    #endregion

    #region Ctor

    public ProductCatalogSyncEventConsumer(
        IShopifyAdminApiService adminApiService,
        IProductService productService,
        ShopifyCheckoutSettings settings)
    {
        _adminApiService = adminApiService;
        _productService = productService;
        _settings = settings;
    }

    #endregion

    #region Event Handlers

    public async Task HandleEventAsync(EntityInsertedEvent<Product> eventMessage)
    {
        if (!_settings.EnableAutoCatalogSync)
            return;

        if (eventMessage?.Entity == null || !eventMessage.Entity.Published || eventMessage.Entity.Deleted)
            return;

        await _adminApiService.CreateOrUpdateProductAsync(eventMessage.Entity);
    }

    public async Task HandleEventAsync(EntityUpdatedEvent<Product> eventMessage)
    {
        if (!_settings.EnableAutoCatalogSync)
            return;

        if (eventMessage?.Entity == null)
            return;

        if (!eventMessage.Entity.Published || eventMessage.Entity.Deleted)
        {
            await _adminApiService.DeleteProductAsync(eventMessage.Entity);
            return;
        }

        await _adminApiService.CreateOrUpdateProductAsync(eventMessage.Entity);
    }

    public async Task HandleEventAsync(EntityDeletedEvent<Product> eventMessage)
    {
        if (!_settings.EnableAutoCatalogSync)
            return;

        if (eventMessage?.Entity == null)
            return;

        await _adminApiService.DeleteProductAsync(eventMessage.Entity);
    }

    public async Task HandleEventAsync(EntityInsertedEvent<ProductAttributeCombination> eventMessage)
    {
        if (!_settings.EnableAutoCatalogSync)
            return;

        if (eventMessage?.Entity == null)
            return;

        var product = await _productService.GetProductByIdAsync(eventMessage.Entity.ProductId);
        if (product != null && product.Published && !product.Deleted)
        {
            await _adminApiService.CreateOrUpdateCombinationAsync(product, eventMessage.Entity);
        }
    }

    public async Task HandleEventAsync(EntityUpdatedEvent<ProductAttributeCombination> eventMessage)
    {
        if (!_settings.EnableAutoCatalogSync)
            return;

        if (eventMessage?.Entity == null)
            return;

        var product = await _productService.GetProductByIdAsync(eventMessage.Entity.ProductId);
        if (product != null && product.Published && !product.Deleted)
        {
            await _adminApiService.CreateOrUpdateCombinationAsync(product, eventMessage.Entity);
        }
    }

    public async Task HandleEventAsync(EntityDeletedEvent<ProductAttributeCombination> eventMessage)
    {
        if (!_settings.EnableAutoCatalogSync)
            return;

        // No action needed for deleted combination beyond parent
    }

    #endregion
}
