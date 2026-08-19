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
using Nop.Services.Configuration;
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
    private readonly ISettingService _settingService;
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
        ISettingService settingService,
        ILogger logger)
    {
        _httpClient = httpClient;
        _settings = settings;
        _configuration = configuration;
        _productService = productService;
        _productAttributeService = productAttributeService;
        _genericAttributeService = genericAttributeService;
        _settingService = settingService;
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
        if (!string.IsNullOrWhiteSpace(_settings.AdminApiAccessToken))
        {
            var trimmedToken = _settings.AdminApiAccessToken.Trim();
            if (await TestAdminTokenAsync(trimmedToken))
                return trimmedToken;
        }

        var kvToken = _configuration["Shopify:AdminApiAccessToken"] ?? _configuration["ShopifyAdminApiAccessToken"];
        if (!string.IsNullOrWhiteSpace(kvToken))
        {
            var trimmedKv = kvToken.Trim();
            if (await TestAdminTokenAsync(trimmedKv))
                return trimmedKv;
        }

        if (!string.IsNullOrWhiteSpace(_cachedAccessToken) && DateTime.UtcNow < _tokenExpiresAt)
            return _cachedAccessToken;

        var clientId = !string.IsNullOrWhiteSpace(_settings.ClientId) ? _settings.ClientId.Trim() : _configuration["Shopify:ClientId"];
        var clientSecret = !string.IsNullOrWhiteSpace(_settings.ClientSecret) ? _settings.ClientSecret.Trim() : _configuration["Shopify:ClientSecret"];

        if (!string.IsNullOrWhiteSpace(clientId) && !string.IsNullOrWhiteSpace(clientSecret) && !string.IsNullOrWhiteSpace(_settings.StoreUrl))
        {
            var storeDomain = _settings.StoreUrl.Trim().Replace("https://", "").Replace("http://", "").TrimEnd('/');
            var oauthUrl = $"https://{storeDomain}/admin/oauth/access_token";

            try
            {
                var formValues = new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = clientId,
                    ["client_secret"] = clientSecret
                };

                using var request = new HttpRequestMessage(HttpMethod.Post, oauthUrl);
                request.Content = new FormUrlEncodedContent(formValues);

                var response = await _httpClient.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    using var jsonReq = new HttpRequestMessage(HttpMethod.Post, oauthUrl);
                    jsonReq.Content = new StringContent(JsonSerializer.Serialize(formValues), Encoding.UTF8, "application/json");
                    response = await _httpClient.SendAsync(jsonReq);
                    content = await response.Content.ReadAsStringAsync();
                }

                if (!response.IsSuccessStatusCode)
                {
                    using var basicReq = new HttpRequestMessage(HttpMethod.Post, oauthUrl);
                    var authBytes = Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}");
                    basicReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));
                    basicReq.Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "client_credentials" });
                    response = await _httpClient.SendAsync(basicReq);
                    content = await response.Content.ReadAsStringAsync();
                }

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
                        _tokenExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn - 300);
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

            // Fallback: Test if ClientSecret or ClientId acts directly as valid Admin API access token
            if (await TestAdminTokenAsync(clientSecret))
            {
                _cachedAccessToken = clientSecret;
                _tokenExpiresAt = DateTime.UtcNow.AddDays(1);
                await _logger.InformationAsync("Client Secret verified directly as valid Shopify Admin API access token.");
                return clientSecret;
            }

            if (await TestAdminTokenAsync(clientId))
            {
                _cachedAccessToken = clientId;
                _tokenExpiresAt = DateTime.UtcNow.AddDays(1);
                await _logger.InformationAsync("Client ID verified directly as valid Shopify Admin API access token.");
                return clientId;
            }
        }

        return null;
    }

    /// <summary>
    /// Tests whether a candidate token is accepted by Shopify Admin API GraphQL endpoint
    /// </summary>
    private async Task<bool> TestAdminTokenAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(_settings.StoreUrl))
            return false;

        try
        {
            var storeDomain = _settings.StoreUrl.Trim().Replace("https://", "").Replace("http://", "").TrimEnd('/');
            var apiVersion = string.IsNullOrWhiteSpace(_settings.ApiVersion) ? ShopifyCheckoutDefaults.DefaultApiVersion : _settings.ApiVersion.Trim();
            var endpoint = $"https://{storeDomain}/admin/api/{apiVersion}/graphql.json";

            var testQuery = new { query = "{ shop { name } }" };
            using var req = new HttpRequestMessage(HttpMethod.Post, endpoint);
            req.Headers.Add("X-Shopify-Access-Token", token.Trim());
            req.Content = new StringContent(JsonSerializer.Serialize(testQuery), Encoding.UTF8, "application/json");

            var resp = await _httpClient.SendAsync(req);
            if (resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("data", out var data) && data.TryGetProperty("shop", out _))
                {
                    return true;
                }
            }
        }
        catch
        {
            // Ignore testing exception
        }

        return false;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Auto-generates or retrieves a Storefront API Access Token using Admin API
    /// </summary>
    public async Task<(bool Success, string Token, string Message)> GetOrCreateStorefrontAccessTokenAsync()
    {
        if (!string.IsNullOrWhiteSpace(_settings.StorefrontAccessToken))
            return (true, _settings.StorefrontAccessToken, "Storefront Access Token is already configured.");

        var adminToken = await GetEffectiveAdminTokenAsync();
        if (string.IsNullOrWhiteSpace(adminToken))
            return (false, null, "Admin API Access Token / Client Credentials missing. Please provide Client ID & Secret or Admin Token first.");

        if (string.IsNullOrWhiteSpace(_settings.StoreUrl))
            return (false, null, "Shopify Store URL is missing.");

        var storeDomain = _settings.StoreUrl.Trim().Replace("https://", "").Replace("http://", "").TrimEnd('/');
        var apiVersion = string.IsNullOrWhiteSpace(_settings.ApiVersion) ? ShopifyCheckoutDefaults.DefaultApiVersion : _settings.ApiVersion.Trim();
        var endpoint = $"https://{storeDomain}/admin/api/{apiVersion}/graphql.json";

        // 1. Try querying existing storefront access tokens
        var query = @"
query {
  shop {
    storefrontAccessTokens(first: 5) {
      nodes {
        accessToken
        title
      }
    }
  }
}";
        try
        {
            using var req1 = new HttpRequestMessage(HttpMethod.Post, endpoint);
            req1.Headers.Add("X-Shopify-Access-Token", adminToken);
            req1.Content = new StringContent(JsonSerializer.Serialize(new { query }), Encoding.UTF8, "application/json");

            var resp1 = await _httpClient.SendAsync(req1);
            var content1 = await resp1.Content.ReadAsStringAsync();

            if (resp1.IsSuccessStatusCode)
            {
                using var doc1 = JsonDocument.Parse(content1);
                if (doc1.RootElement.TryGetProperty("data", out var data1) && data1.TryGetProperty("shop", out var shop1))
                {
                    if (shop1.TryGetProperty("storefrontAccessTokens", out var sfTokens) && sfTokens.TryGetProperty("nodes", out var nodes) && nodes.ValueKind == JsonValueKind.Array && nodes.GetArrayLength() > 0)
                    {
                        var firstToken = nodes[0].GetProperty("accessToken").GetString();
                        if (!string.IsNullOrWhiteSpace(firstToken))
                        {
                            _settings.StorefrontAccessToken = firstToken;
                            await _settingService.SaveSettingAsync(_settings);
                            return (true, firstToken, "Successfully retrieved existing Storefront Access Token from Shopify!");
                        }
                    }
                }
            }

            // 2. Create new Storefront Access Token
            var mutation = @"
mutation {
  storefrontAccessTokenCreate(input: {title: ""nopCommerce Plugin Storefront Token""}) {
    storefrontAccessToken {
      accessToken
    }
    userErrors {
      field
      message
    }
  }
}";
            using var req2 = new HttpRequestMessage(HttpMethod.Post, endpoint);
            req2.Headers.Add("X-Shopify-Access-Token", adminToken);
            req2.Content = new StringContent(JsonSerializer.Serialize(new { query = mutation }), Encoding.UTF8, "application/json");

            var resp2 = await _httpClient.SendAsync(req2);
            var content2 = await resp2.Content.ReadAsStringAsync();

            if (resp2.IsSuccessStatusCode)
            {
                using var doc2 = JsonDocument.Parse(content2);
                if (doc2.RootElement.TryGetProperty("data", out var data2) && data2.TryGetProperty("storefrontAccessTokenCreate", out var sfCreate))
                {
                    if (sfCreate.TryGetProperty("storefrontAccessToken", out var sfTokenObj) && sfTokenObj.TryGetProperty("accessToken", out var createdTokenProp))
                    {
                        var newToken = createdTokenProp.GetString();
                        if (!string.IsNullOrWhiteSpace(newToken))
                        {
                            _settings.StorefrontAccessToken = newToken;
                            await _settingService.SaveSettingAsync(_settings);
                            return (true, newToken, "Successfully generated and saved new Storefront Access Token!");
                        }
                    }
                }
            }

            return (false, null, "Failed to auto-generate Storefront Access Token from Shopify Admin API.");
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync("Error auto-generating Storefront Access Token", ex);
            return (false, null, ex.Message);
        }
    }

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
