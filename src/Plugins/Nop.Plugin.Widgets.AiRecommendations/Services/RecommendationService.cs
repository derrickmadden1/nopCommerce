using Microsoft.Extensions.Logging;
using Nop.Core.Domain.Catalog;
using Nop.Services.Catalog;
using Nop.Services.Orders;

namespace Nop.Plugin.Widgets.AiRecommendations.Services;

public class RecommendationService
{
    private readonly EmbeddingService _embeddingService;
    private readonly IProductService _productService;
    private readonly IOrderService _orderService;
    private readonly AiRecommendationsSettings _settings;
    private readonly ILogger<RecommendationService> _logger;

    public RecommendationService(
        EmbeddingService embeddingService,
        IProductService productService,
        IOrderService orderService,
        AiRecommendationsSettings settings,
        ILogger<RecommendationService> logger)
    {
        _embeddingService = embeddingService;
        _productService = productService;
        _orderService = orderService;
        _settings = settings;
        _logger = logger;
    }

    /// <summary>
    /// Similar products — based on cosine similarity to the given product.
    /// Used on product pages.
    /// </summary>
    public async Task<IList<Product>> GetSimilarProductsAsync(int productId, int count = 0)
    {
        count = count > 0 ? count : _settings.RecommendationCount;

        try
        {
            var targetEmbedding = await _embeddingService.GetProductEmbeddingAsync(productId);
            if (targetEmbedding == null) return new List<Product>();

            var allEmbeddings = await _embeddingService.GetAllEmbeddingsAsync();

            var scored = allEmbeddings
                .Where(kv => kv.Key != productId)
                .Select(kv => (ProductId: kv.Key, Score: CosineSimilarity(targetEmbedding, kv.Value)))
                .Where(x => x.Score >= _settings.MinSimilarityScore)
                .OrderByDescending(x => x.Score)
                .Take(count)
                .ToList();

            return await LoadProductsInOrderAsync(scored.Select(x => x.ProductId).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get similar products for product {ProductId}", productId);
            return new List<Product>();
        }
    }

    /// <summary>
    /// Personalised recommendations — based on a customer's purchase history.
    /// Used on homepage and cart page.
    /// </summary>
    public async Task<IList<Product>> GetPersonalisedRecommendationsAsync(int customerId, int count = 0,
        IList<int>? excludeProductIds = null)
    {
        count = count > 0 ? count : _settings.RecommendationCount;

        try
        {
            // Get product IDs the customer has previously ordered
            var orders = await _orderService.SearchOrdersAsync(customerId: customerId);
            var purchasedProductIds = new HashSet<int>();

            foreach (var order in orders.Take(10)) // Last 10 orders is sufficient
            {
                var items = await _orderService.GetOrderItemsAsync(order.Id);
                foreach (var item in items)
                    purchasedProductIds.Add(item.ProductId);
            }

            if (!purchasedProductIds.Any())
                return await GetPopularProductsAsync(count); // Fallback for new customers

            // Build centroid embedding from all purchased product embeddings
            var purchaseEmbeddings = new List<float[]>();
            foreach (var pid in purchasedProductIds)
            {
                var embedding = await _embeddingService.GetProductEmbeddingAsync(pid);
                if (embedding != null)
                    purchaseEmbeddings.Add(embedding);
            }

            if (!purchaseEmbeddings.Any())
                return await GetPopularProductsAsync(count);

            var centroid = ComputeCentroid(purchaseEmbeddings);
            var allEmbeddings = await _embeddingService.GetAllEmbeddingsAsync();

            var excludeIds = new HashSet<int>(purchasedProductIds);
            if (excludeProductIds != null)
                foreach (var id in excludeProductIds)
                    excludeIds.Add(id);

            var scored = allEmbeddings
                .Where(kv => !excludeIds.Contains(kv.Key))
                .Select(kv => (ProductId: kv.Key, Score: CosineSimilarity(centroid, kv.Value)))
                .Where(x => x.Score >= _settings.MinSimilarityScore)
                .OrderByDescending(x => x.Score)
                .Take(count)
                .ToList();

            return await LoadProductsInOrderAsync(scored.Select(x => x.ProductId).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get personalised recommendations for customer {CustomerId}", customerId);
            return new List<Product>();
        }
    }

    /// <summary>
    /// Fallback for anonymous visitors or customers with no purchase history
    /// Returns newest published products
    /// </summary>
    private async Task<IList<Product>> GetPopularProductsAsync(int count)
    {
        var products = await _productService.SearchProductsAsync(
            pageSize: count,
            orderBy: ProductSortingEnum.CreatedOn,
            overridePublished: true
        );

        return products.ToList();
    }

    private async Task<IList<Product>> LoadProductsInOrderAsync(IList<int> productIds)
    {
        if (!productIds.Any()) return new List<Product>();

        var products = await _productService.GetProductsByIdsAsync(productIds.ToArray());

        // Preserve similarity order and filter out deleted/unpublished
        return productIds
            .Select(id => products.FirstOrDefault(p => p.Id == id))
            .Where(p => p != null && !p.Deleted && p.Published)
            .Cast<Product>()
            .ToList();
    }

    /// <summary>
    /// Cosine similarity between two vectors — returns 0.0 to 1.0
    /// </summary>
    private static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0;

        double dot = 0, magA = 0, magB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            magA += a[i] * a[i];
            magB += b[i] * b[i];
        }

        if (magA == 0 || magB == 0) return 0;
        return dot / (Math.Sqrt(magA) * Math.Sqrt(magB));
    }

    /// <summary>
    /// Computes the average (centroid) of a list of embedding vectors
    /// </summary>
    private static float[] ComputeCentroid(List<float[]> embeddings)
    {
        var length = embeddings[0].Length;
        var centroid = new float[length];

        foreach (var embedding in embeddings)
            for (int i = 0; i < length; i++)
                centroid[i] += embedding[i];

        for (int i = 0; i < length; i++)
            centroid[i] /= embeddings.Count;

        return centroid;
    }
}
