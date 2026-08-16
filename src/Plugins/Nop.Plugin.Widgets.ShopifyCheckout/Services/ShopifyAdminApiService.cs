using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Nop.Core.Domain.Catalog;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Logging;

namespace Nop.Plugin.Widgets.ShopifyCheckout.Services;

/// <summary>
/// Represents the Shopify Admin API catalog synchronization service
/// </summary>
public class ShopifyAdminApiService : IShopifyAdminApiService
{
    #region Fields

    private readonly HttpClient _httpClient;
    private readonly ShopifyCheckoutSettings _settings;
    private readonly IConfiguration _configuration;
    private readonly IProductService _productService;
    private readonly IProductAttributeService _productAttributeService;
    private readonly IGenericAttributeService _genericAttributeService;
    private readonly ILogger _logger;

    private static string _cachedAccessToken;
    private static DateTime _tokenExpiresAt = DateTime.MinValue;

    #endregion

    #region Ctor

    public ShopifyAdminApiService(
        HttpClient httpClient,
        ShopifyCheckoutSettings settings,
        IConfiguration configuration,
        IProductService productService,
        IProductAttributeService productAttributeService,
        IGenericAttributeService genericAttributeService,
        ILogger logger)
    {
        _httpClient = httpClient;
        _settings = settings;
        _configuration = configuration;
        _productService = productService;
        _productAttributeService = productAttributeService;
        _genericAttributeService = genericAttributeService;
        _logger = logger;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Gets the effective Admin API Access token.
    /// Checks explicit settings first, then Azure Key Vault / IConfiguration, then executes OAuth client_credentials grant if Client ID/Secret are provided.
    /// </summary>
    private async Task<string> GetEffectiveAdminTokenAsync()
    {
        // 1. Check explicit setting
        if (!string.IsNullOrWhiteSpace(_settings.AdminApiAccessToken))
            return _settings.AdminApiAccessToken.Trim();

        // 2. Check Azure Key Vault / IConfiguration fallback
        var kvToken = _configuration["Shopify:AdminApiAccessToken"] ?? _configuration["ShopifyAdminApiAccessToken"];
        if (!string.IsNullOrWhiteSpace(kvToken))
            return kvToken.Trim();

        // 3. Check memory cache for client_credentials access token
        if (!string.IsNullOrWhiteSpace(_cachedAccessToken) && DateTime.UtcNow < _tokenExpiresAt)
            return _cachedAccessToken;

        // 4. Try client_credentials grant exchange using Client ID + Client Secret
        var clientId = !string.IsNullOrWhiteSpace(_settings.ClientId) ? _settings.ClientId.Trim() : _configuration["Shopify:ClientId"];
        var clientSecret = !string.IsNullOrWhiteSpace(_settings.ClientSecret) ? _settings.ClientSecret.Trim() : _configuration["Shopify:ClientSecret"];

        if (!string.IsNullOrWhiteSpace(clientId) && !string.IsNullOrWhiteSpace(clientSecret) && !string.IsNullOrWhiteSpace(_settings.StoreUrl))
        {
            var storeDomain = _settings.StoreUrl.Trim().Replace("https://", "").Replace("http://", "").TrimEnd('/');
            var oauthUrl = $"https://{storeDomain}/admin/oauth/access_token";

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, oauthUrl);
                var formValues = new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = clientId,
                    ["client_secret"] = clientSecret
                };
                request.Content = new FormUrlEncodedContent(formValues);

                var response = await _httpClient.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(content);
                    if (doc.RootElement.TryGetProperty("access_token", out var tokenProp))
                    {
                        var token = tokenProp.GetString();
                        int expiresIn = 86399;
                        if (doc.RootElement.TryGetProperty("expires_in", out var expiresProp) && expiresProp.TryGetInt32(out var exp))
                            expiresIn = exp;

                        _cachedAccessToken = token;
                        _tokenExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn - 300); // 5 min safety margin
                        await _logger.InformationAsync("Successfully retrieved Shopify Admin API access token via client_credentials grant.");
                        return token;
                    }
                }
                else
                {
                    await _logger.WarningAsync($"Shopify OAuth client_credentials grant failed HTTP {response.StatusCode}: {content}");
                }
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync("Error exchanging Client ID and Secret for Shopify access token", ex);
            }
        }

        return null;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Pushes a product to Shopify Admin API and saves the resulting Variant GID to GenericAttributes
    /// </summary>
    public async Task<(bool Success, string VariantGid, string Message)> CreateOrUpdateProductAsync(Product product)
    {
        if (product == null)
            return (false, null, "Product is null");

        var token = await GetEffectiveAdminTokenAsync();
        if (string.IsNullOrWhiteSpace(token))
            return (false, null, "Shopify Admin API Access Token is not configured (checked Plugin Settings, Key Vault, and Client Credentials grant).");

        if (string.IsNullOrWhiteSpace(_settings.StoreUrl))
            return (false, null, "Shopify Store URL is not configured.");

        var storeDomain = _settings.StoreUrl.Trim().Replace("https://", "").Replace("http://", "").TrimEnd('/');
        var apiVersion = string.IsNullOrWhiteSpace(_settings.ApiVersion) ? ShopifyCheckoutDefaults.DefaultApiVersion : _settings.ApiVersion.Trim();
        var endpoint = $"https://{storeDomain}/admin/api/{apiVersion}/graphql.json";

        var priceStr = product.Price.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        var skuStr = string.IsNullOrWhiteSpace(product.Sku) ? $"NOP-{product.Id}" : product.Sku.Trim();

        var mutation = @"
mutation productSet($input: ProductSetInput!) {
  productSet(synchronous: true, input: $input) {
    product {
      id
      title
      variants(first: 10) {
        nodes {
          id
          title
          sku
          price
        }
      }
    }
    userErrors {
      field
      message
    }
  }
}";

        var requestPayload = new
        {
            query = mutation,
            variables = new
            {
                input = new
                {
                    title = product.Name,
                    descriptionHtml = product.FullDescription ?? product.ShortDescription ?? product.Name,
                    vendor = "nopCommerce",
                    productOptions = new[]
                    {
                        new
                        {
                            name = "Title",
                            values = new[] { new { name = "Default Title" } }
                        }
                    },
                    variants = new[]
                    {
                        new
                        {
                            sku = skuStr,
                            price = priceStr,
                            optionValues = new[]
                            {
                                new
                                {
                                    name = "Default Title",
                                    optionName = "Title"
                                }
                            }
                        }
                    }
                }
            }
        };

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.Add("X-Shopify-Access-Token", token);
            request.Content = new StringContent(JsonSerializer.Serialize(requestPayload), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return (false, null, $"Shopify Admin API returned HTTP {response.StatusCode}: {responseContent}");
            }

            using var doc = JsonDocument.Parse(responseContent);
            var root = doc.RootElement;

            if (root.TryGetProperty("data", out var data) && data.TryGetProperty("productSet", out var productSet))
            {
                if (productSet.TryGetProperty("userErrors", out var userErrors) && userErrors.ValueKind == JsonValueKind.Array && userErrors.GetArrayLength() > 0)
                {
                    var errMsg = string.Join("; ", userErrors.EnumerateArray().Select(e => e.GetProperty("message").GetString()));
                    return (false, null, $"Shopify GraphQL user errors: {errMsg}");
                }

                if (productSet.TryGetProperty("product", out var shopifyProduct) && shopifyProduct.TryGetProperty("variants", out var variants))
                {
                    if (variants.TryGetProperty("nodes", out var nodes) && nodes.ValueKind == JsonValueKind.Array && nodes.GetArrayLength() > 0)
                    {
                        var firstVariant = nodes[0];
                        if (firstVariant.TryGetProperty("id", out var variantIdProp))
                        {
                            var variantGid = variantIdProp.GetString();
                            if (!string.IsNullOrWhiteSpace(variantGid))
                            {
                                await _genericAttributeService.SaveAttributeAsync(product, ShopifyCheckoutDefaults.ShopifyVariantIdAttribute, variantGid);
                                return (true, variantGid, $"Successfully synced product '{product.Name}' to Shopify. Variant GID: {variantGid}");
                            }
                        }
                    }
                }
            }

            return (false, null, "Shopify Admin API did not return a valid Variant GID.");
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync($"Error pushing product #{product.Id} '{product.Name}' to Shopify Admin API", ex);
            return (false, null, ex.Message);
        }
    }

    /// <summary>
    /// Pushes a product attribute combination to Shopify and maps its Variant GID
    /// </summary>
    public async Task<(bool Success, string VariantGid, string Message)> CreateOrUpdateCombinationAsync(Product product, ProductAttributeCombination combination)
    {
        if (product == null || combination == null)
            return (false, null, "Product or combination is null");

        var result = await CreateOrUpdateProductAsync(product);
        if (result.Success && !string.IsNullOrWhiteSpace(result.VariantGid))
        {
            await _genericAttributeService.SaveAttributeAsync(combination, ShopifyCheckoutDefaults.ShopifyVariantIdAttribute, result.VariantGid);
        }

        return result;
    }

    /// <summary>
    /// Deletes a product mapping from Shopify Admin API
    /// </summary>
    public async Task<(bool Success, string Message)> DeleteProductAsync(Product product)
    {
        if (product == null)
            return (false, "Product is null");

        await _genericAttributeService.SaveAttributeAsync<string>(product, ShopifyCheckoutDefaults.ShopifyVariantIdAttribute, null);
        return (true, $"Deleted Shopify variant mapping for product #{product.Id}");
    }

    /// <summary>
    /// Executes a full catalog sync pushing all active nopCommerce products & combinations to Shopify
    /// </summary>
    public async Task<(int TotalProcessed, int SyncedCount, int FailedCount, List<string> Logs)> FullCatalogSyncAsync()
    {
        var logs = new List<string>();
        int totalProcessed = 0;
        int syncedCount = 0;
        int failedCount = 0;

        var token = await GetEffectiveAdminTokenAsync();
        if (string.IsNullOrWhiteSpace(token))
        {
            logs.Add("ERROR: Shopify Admin API Access Token could not be retrieved.");
            return (0, 0, 0, logs);
        }

        logs.Add("Starting full nopCommerce catalog sync to Shopify...");

        int pageIndex = 0;
        int pageSize = 50;

        while (true)
        {
            var products = await _productService.SearchProductsAsync(pageIndex: pageIndex, pageSize: pageSize, showHidden: false);
            if (!products.Any())
                break;

            foreach (var product in products)
            {
                totalProcessed++;

                var combinations = await _productAttributeService.GetAllProductAttributeCombinationsAsync(product.Id);
                if (combinations != null && combinations.Any())
                {
                    foreach (var comb in combinations)
                    {
                        var combResult = await CreateOrUpdateCombinationAsync(product, comb);
                        if (combResult.Success)
                        {
                            syncedCount++;
                            logs.Add($"SUCCESS: Synced combination SKU '{comb.Sku}' -> GID {combResult.VariantGid}");
                        }
                        else
                        {
                            failedCount++;
                            logs.Add($"FAILED: Combination SKU '{comb.Sku}' - {combResult.Message}");
                        }
                    }
                }
                else
                {
                    var prodResult = await CreateOrUpdateProductAsync(product);
                    if (prodResult.Success)
                    {
                        syncedCount++;
                        logs.Add($"SUCCESS: Synced product '{product.Name}' -> GID {prodResult.VariantGid}");
                    }
                    else
                    {
                        failedCount++;
                        logs.Add($"FAILED: Product '{product.Name}' - {prodResult.Message}");
                    }
                }
            }

            pageIndex++;
            if (products.HasNextPage == false)
                break;
        }

        var summary = $"Completed full catalog sync. Processed: {totalProcessed}, Synced: {syncedCount}, Failed: {failedCount}";
        logs.Add(summary);
        await _logger.InformationAsync(summary);

        return (totalProcessed, syncedCount, failedCount, logs);
    }

    #endregion
}
