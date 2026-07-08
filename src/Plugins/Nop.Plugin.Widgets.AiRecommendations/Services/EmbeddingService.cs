using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Logging;
using Nop.Data;
using Nop.Plugin.Widgets.AiRecommendations.Data;
using Nop.Services.Catalog;

namespace Nop.Plugin.Widgets.AiRecommendations.Services;

public class EmbeddingService
{
    private readonly AiRecommendationsSettings _settings;
    private readonly IRepository<ProductEmbeddingRecord> _embeddingRepository;
    private readonly IProductService _productService;
    private readonly ICategoryService _categoryService;
    private readonly ILogger<EmbeddingService> _logger;

    public EmbeddingService(
        AiRecommendationsSettings settings,
        IRepository<ProductEmbeddingRecord> embeddingRepository,
        IProductService productService,
        ICategoryService categoryService,
        ILogger<EmbeddingService> logger)
    {
        _settings = settings;
        _embeddingRepository = embeddingRepository;
        _productService = productService;
        _categoryService = categoryService;
        _logger = logger;
    }

    /// <summary>
    /// Generates or refreshes embeddings for all published products.
    /// Skips products whose content hasn't changed since last embedding.
    /// </summary>
    public async Task GenerateAllEmbeddingsAsync()
    {
        var products = await _productService.SearchProductsAsync(
            pageSize: int.MaxValue,
            overridePublished: true
        );

        var client = await GetOpenAIClientAsync();
        var processed = 0;
        var skipped = 0;

        foreach (var product in products)
        {
            try
            {
                var content = BuildEmbeddingContent(product.Name, product.ShortDescription, product.FullDescription);
                var hash = ComputeHash(content);

                // Check if we already have an up-to-date embedding
                var existing = await GetEmbeddingRecordAsync(product.Id);
                if (existing != null && existing.ContentHash == hash)
                {
                    skipped++;
                    continue;
                }

                var embedding = await GenerateEmbeddingAsync(client, content);
                if (embedding == null) continue;

                await UpsertEmbeddingAsync(product.Id, embedding, hash);
                processed++;

                // Brief pause to avoid hitting rate limits
                await Task.Delay(100);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate embedding for product {ProductId}", product.Id);
            }
        }

        _logger.LogInformation("Embedding generation complete. Processed: {Processed}, Skipped (unchanged): {Skipped}",
            processed, skipped);
    }

    /// <summary>
    /// Gets the embedding vector for a single product, generating it if needed
    /// </summary>
    public async Task<float[]?> GetProductEmbeddingAsync(int productId)
    {
        var record = await GetEmbeddingRecordAsync(productId);
        if (record == null) return null;

        return JsonSerializer.Deserialize<float[]>(record.EmbeddingJson);
    }

    /// <summary>
    /// Gets all stored embeddings — used for similarity comparison
    /// </summary>
    public async Task<Dictionary<int, float[]>> GetAllEmbeddingsAsync()
    {
        var records = await _embeddingRepository.GetAllAsync(q => q);
        var result = new Dictionary<int, float[]>();

        foreach (var record in records)
        {
            var vector = JsonSerializer.Deserialize<float[]>(record.EmbeddingJson);
            if (vector != null)
                result[record.ProductId] = vector;
        }

        return result;
    }

    private async Task<float[]?> GenerateEmbeddingAsync(OpenAIClient client, string content)
    {
        try
        {
            var options = new EmbeddingsOptions(_settings.EmbeddingDeploymentName, new[] { content });
            var response = await client.GetEmbeddingsAsync(options);
            return response.Value.Data[0].Embedding.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Azure OpenAI embedding call failed");
            return null;
        }
    }

    private async Task UpsertEmbeddingAsync(int productId, float[] embedding, string hash)
    {
        var existing = await GetEmbeddingRecordAsync(productId);
        var json = JsonSerializer.Serialize(embedding);

        if (existing != null)
        {
            existing.EmbeddingJson = json;
            existing.ContentHash = hash;
            existing.GeneratedAtUtc = DateTime.UtcNow;
            await _embeddingRepository.UpdateAsync(existing);
        }
        else
        {
            await _embeddingRepository.InsertAsync(new ProductEmbeddingRecord
            {
                ProductId = productId,
                EmbeddingJson = json,
                ContentHash = hash,
                GeneratedAtUtc = DateTime.UtcNow
            });
        }
    }

    private async Task<ProductEmbeddingRecord?> GetEmbeddingRecordAsync(int productId)
    {
        return await _embeddingRepository.GetAllAsync(q =>
            q.Where(r => r.ProductId == productId))
            .ContinueWith(t => t.Result.FirstOrDefault());
    }

    private static string BuildEmbeddingContent(string name, string? shortDesc, string? fullDesc)
    {
        var sb = new StringBuilder();
        sb.Append(name);
        if (!string.IsNullOrWhiteSpace(shortDesc))
            sb.Append(". ").Append(StripHtml(shortDesc));
        if (!string.IsNullOrWhiteSpace(fullDesc))
            sb.Append(". ").Append(StripHtml(fullDesc));
        return sb.ToString();
    }

    private static string StripHtml(string input)
        => System.Text.RegularExpressions.Regex.Replace(input, "<.*?>", " ").Trim();

    private static string ComputeHash(string content)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private async Task<OpenAIClient> GetOpenAIClientAsync()
    {
        var apiKey = _settings.AzureOpenAIApiKey;

        if (_settings.UseAzureKeyVault)
        {
            if (string.IsNullOrWhiteSpace(_settings.AzureKeyVaultUrl) || string.IsNullOrWhiteSpace(_settings.AzureKeyVaultSecretName))
            {
                throw new Exception("Azure Key Vault is enabled but URL or Secret Name is not configured.");
            }

            var secretClient = new SecretClient(new Uri(_settings.AzureKeyVaultUrl), new DefaultAzureCredential());
            var secretResponse = await secretClient.GetSecretAsync(_settings.AzureKeyVaultSecretName);
            apiKey = secretResponse.Value.Value;
        }

        return new OpenAIClient(new Uri(_settings.AzureOpenAIEndpoint), new AzureKeyCredential(apiKey));
    }
}
