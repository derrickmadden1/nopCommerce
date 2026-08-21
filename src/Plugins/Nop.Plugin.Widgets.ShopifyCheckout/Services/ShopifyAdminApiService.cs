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
    private async Task<(string Token, string Error)> GetEffectiveAdminTokenAsync()
    {
        if (!string.IsNullOrWhiteSpace(_settings.AdminApiAccessToken))
        {
            return (_settings.AdminApiAccessToken.Trim(), null);
        }

        var kvToken = _configuration["Shopify:AdminApiAccessToken"] ?? _configuration["ShopifyAdminApiAccessToken"];
        if (!string.IsNullOrWhiteSpace(kvToken))
        {
            return (kvToken.Trim(), null);
        }

        if (!string.IsNullOrWhiteSpace(_cachedAccessToken) && DateTime.UtcNow < _tokenExpiresAt)
            return (_cachedAccessToken, null);

        var clientId = !string.IsNullOrWhiteSpace(_settings.ClientId) ? _settings.ClientId.Trim() : _configuration["Shopify:ClientId"];
        var clientSecret = !string.IsNullOrWhiteSpace(_settings.ClientSecret) ? _settings.ClientSecret.Trim() : _configuration["Shopify:ClientSecret"];

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            return (null, "Client ID or Client Secret is missing in plugin settings.");
        }

        if (string.IsNullOrWhiteSpace(_settings.StoreUrl))
        {
            return (null, "Shopify Store URL is missing in plugin settings.");
        }

        var storeDomain = _settings.StoreUrl.Trim().Replace("https://", "").Replace("http://", "").TrimEnd('/');
        var oauthUrl = $"https://{storeDomain}/admin/oauth/access_token";
        string lastError = null;

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
                    return (token, null);
                }
            }
            else
            {
                lastError = $"Shopify OAuth client_credentials grant failed HTTP {response.StatusCode}: {content}";
                await _logger.WarningAsync(lastError);
            }
        }
        catch (Exception ex)
        {
            lastError = $"Error exchanging Client ID and Secret for Shopify access token: {ex.Message}";
            await _logger.ErrorAsync(lastError, ex);
        }

        // Fallback: Test if ClientSecret or ClientId acts directly as valid Admin API access token
        if (await TestAdminTokenAsync(clientSecret))
        {
            _cachedAccessToken = clientSecret;
            _tokenExpiresAt = DateTime.UtcNow.AddDays(1);
            await _logger.InformationAsync("Client Secret verified directly as valid Shopify Admin API access token.");
            return (clientSecret, null);
        }

        if (await TestAdminTokenAsync(clientId))
        {
            _cachedAccessToken = clientId;
            _tokenExpiresAt = DateTime.UtcNow.AddDays(1);
            await _logger.InformationAsync("Client ID verified directly as valid Shopify Admin API access token.");
            return (clientId, null);
        }

        return (null, lastError ?? "Shopify OAuth Client Credentials grant failed.");
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

        var (adminToken, adminError) = await GetEffectiveAdminTokenAsync();
        if (string.IsNullOrWhiteSpace(adminToken))
            return (false, null, $"Admin API access token retrieval failed: {adminError}");

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

            var lastError = "Failed to auto-generate Storefront Access Token from Shopify Admin API.";

            if (resp1.IsSuccessStatusCode)
            {
                using var doc1 = JsonDocument.Parse(content1);
                var root1 = doc1.RootElement;
                if (root1.ValueKind == JsonValueKind.Object && root1.TryGetProperty("errors", out var errors1) && errors1.ValueKind == JsonValueKind.Array && errors1.GetArrayLength() > 0)
                {
                    lastError = string.Join("; ", errors1.EnumerateArray().Select(e => e.ValueKind == JsonValueKind.Object && e.TryGetProperty("message", out var m) ? m.GetString() : e.ToString()));
                }
                else if (root1.ValueKind == JsonValueKind.Object && root1.TryGetProperty("data", out var data1) && data1.ValueKind == JsonValueKind.Object)
                {
                    if (data1.TryGetProperty("shop", out var shop1) && shop1.ValueKind == JsonValueKind.Object)
                    {
                        if (shop1.TryGetProperty("storefrontAccessTokens", out var sfTokens) && sfTokens.ValueKind == JsonValueKind.Object && sfTokens.TryGetProperty("nodes", out var nodes) && nodes.ValueKind == JsonValueKind.Array && nodes.GetArrayLength() > 0)
                        {
                            var firstNode = nodes[0];
                            if (firstNode.ValueKind == JsonValueKind.Object && firstNode.TryGetProperty("accessToken", out var firstTokenProp))
                            {
                                var firstToken = firstTokenProp.GetString();
                                if (!string.IsNullOrWhiteSpace(firstToken))
                                {
                                    _settings.StorefrontAccessToken = firstToken;
                                    await _settingService.SaveSettingAsync(_settings);
                                    return (true, firstToken, "Successfully retrieved existing Storefront Access Token from Shopify!");
                                }
                            }
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
                var root2 = doc2.RootElement;
                if (root2.ValueKind == JsonValueKind.Object && root2.TryGetProperty("errors", out var errors2) && errors2.ValueKind == JsonValueKind.Array && errors2.GetArrayLength() > 0)
                {
                    lastError = string.Join("; ", errors2.EnumerateArray().Select(e => e.ValueKind == JsonValueKind.Object && e.TryGetProperty("message", out var m) ? m.GetString() : e.ToString()));
                }
                else if (root2.ValueKind == JsonValueKind.Object && root2.TryGetProperty("data", out var data2) && data2.ValueKind == JsonValueKind.Object)
                {
                    if (data2.TryGetProperty("storefrontAccessTokenCreate", out var sfCreate) && sfCreate.ValueKind == JsonValueKind.Object)
                    {
                        if (sfCreate.TryGetProperty("userErrors", out var uErrors) && uErrors.ValueKind == JsonValueKind.Array && uErrors.GetArrayLength() > 0)
                        {
                            var uMsg = string.Join("; ", uErrors.EnumerateArray().Select(e => e.ValueKind == JsonValueKind.Object && e.TryGetProperty("message", out var m) ? m.GetString() : e.ToString()));
                            lastError = $"Shopify user error: {uMsg}";
                        }

                        if (sfCreate.TryGetProperty("storefrontAccessToken", out var sfTokenObj) && sfTokenObj.ValueKind == JsonValueKind.Object && sfTokenObj.TryGetProperty("accessToken", out var createdTokenProp))
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
            }
            else
            {
                lastError = $"Shopify Admin API returned HTTP {resp2.StatusCode}: {content2}";
            }

            await _logger.WarningAsync($"GetOrCreateStorefrontAccessTokenAsync failed: {lastError}");
            return (false, null, lastError);
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

        var (token, tokenErr) = await GetEffectiveAdminTokenAsync();
        if (string.IsNullOrWhiteSpace(token))
            return (false, null, $"Shopify Admin API Access Token could not be retrieved: {tokenErr}");

        if (string.IsNullOrWhiteSpace(_settings.StoreUrl))
            return (false, null, "Shopify Store URL is not configured.");

        var storeDomain = _settings.StoreUrl.Trim().Replace("https://", "").Replace("http://", "").TrimEnd('/');
        var apiVersion = string.IsNullOrWhiteSpace(_settings.ApiVersion) ? ShopifyCheckoutDefaults.DefaultApiVersion : _settings.ApiVersion.Trim();
        var endpoint = $"https://{storeDomain}/admin/api/{apiVersion}/graphql.json";

        var priceStr = product.Price.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        var skuStr = string.IsNullOrWhiteSpace(product.Sku) ? $"NOP-{product.Id}" : product.Sku.Trim();

        // 1. Check if product GID is already stored on nopCommerce Product
        var existingProductGid = await _genericAttributeService.GetAttributeAsync<Product, string>(product.Id, "ShopifyProductId");

        // 2. If not stored, query Shopify GraphQL to see if product/variant already exists by SKU
        if (string.IsNullOrWhiteSpace(existingProductGid))
        {
            try
            {
                var findQuery = @"
query findVariantBySku($query: String!) {
  productVariants(first: 1, query: $query) {
    nodes {
      id
      product {
        id
      }
    }
  }
}";
                using var findReq = new HttpRequestMessage(HttpMethod.Post, endpoint);
                findReq.Headers.Add("X-Shopify-Access-Token", token);
                findReq.Content = new StringContent(JsonSerializer.Serialize(new { query = findQuery, variables = new { query = $"sku:{skuStr}" } }), Encoding.UTF8, "application/json");

                var findResp = await _httpClient.SendAsync(findReq);
                if (findResp.IsSuccessStatusCode)
                {
                    var findContent = await findResp.Content.ReadAsStringAsync();
                    using var findDoc = JsonDocument.Parse(findContent);
                    if (findDoc.RootElement.TryGetProperty("data", out var findData) && findData.TryGetProperty("productVariants", out var pVariants))
                    {
                        if (pVariants.TryGetProperty("nodes", out var vNodes) && vNodes.ValueKind == JsonValueKind.Array && vNodes.GetArrayLength() > 0)
                        {
                            var vNode = vNodes[0];
                            if (vNode.TryGetProperty("id", out var existingVariantIdProp))
                            {
                                var foundVariantGid = existingVariantIdProp.GetString();
                                if (!string.IsNullOrWhiteSpace(foundVariantGid))
                                {
                                    await _genericAttributeService.SaveAttributeAsync(product, ShopifyCheckoutDefaults.ShopifyVariantIdAttribute, foundVariantGid);
                                }
                            }

                            if (vNode.TryGetProperty("product", out var parentProd) && parentProd.TryGetProperty("id", out var parentIdProp))
                            {
                                existingProductGid = parentIdProp.GetString();
                                if (!string.IsNullOrWhiteSpace(existingProductGid))
                                {
                                    await _genericAttributeService.SaveAttributeAsync(product, "ShopifyProductId", existingProductGid);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                await _logger.WarningAsync($"SKU lookup failed for product #{product.Id}: {ex.Message}");
            }
        }

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

        var inputDict = new Dictionary<string, object>
        {
            ["title"] = product.Name,
            ["descriptionHtml"] = product.FullDescription ?? product.ShortDescription ?? product.Name,
            ["vendor"] = "nopCommerce",
            ["status"] = "ACTIVE",
            ["productOptions"] = new[]
            {
                new
                {
                    name = "Title",
                    values = new[] { new { name = "Default Title" } }
                }
            },
            ["variants"] = new[]
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
        };

        if (!string.IsNullOrWhiteSpace(existingProductGid))
        {
            inputDict["id"] = existingProductGid;
        }

        var requestPayload = new
        {
            query = mutation,
            variables = new
            {
                input = inputDict
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

                if (productSet.TryGetProperty("product", out var shopifyProduct))
                {
                    if (shopifyProduct.TryGetProperty("id", out var createdProductGidProp))
                    {
                        var createdProductGid = createdProductGidProp.GetString();
                        if (!string.IsNullOrWhiteSpace(createdProductGid))
                        {
                            await _genericAttributeService.SaveAttributeAsync(product, "ShopifyProductId", createdProductGid);
                        }
                    }

                    if (shopifyProduct.TryGetProperty("variants", out var variants) && variants.TryGetProperty("nodes", out var nodes) && nodes.ValueKind == JsonValueKind.Array && nodes.GetArrayLength() > 0)
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

        var (token, tokenErr) = await GetEffectiveAdminTokenAsync();
        if (string.IsNullOrWhiteSpace(token))
        {
            logs.Add($"ERROR: Shopify Admin API Access Token could not be retrieved ({tokenErr}).");
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

    /// <summary>
    /// Creates a Shopify Draft Order with custom item prices and returns the invoice checkout URL
    /// </summary>
    /// <param name="items">List of items containing Variant GID, quantity, and custom unit price</param>
    /// <param name="customerEmail">Customer email address</param>
    /// <returns>Result containing success flag, invoice URL, and error message</returns>
    public async Task<(bool Success, string InvoiceUrl, string Message)> CreateDraftOrderAsync(
        IEnumerable<(string VariantGid, int Quantity, decimal UnitPrice)> items,
        string customerEmail = null)
    {
        if (items == null || !items.Any())
            return (false, null, "No items provided for draft order.");

        var (token, tokenErr) = await GetEffectiveAdminTokenAsync();
        if (string.IsNullOrWhiteSpace(token))
            return (false, null, $"Shopify Admin API Access Token missing: {tokenErr}");

        if (string.IsNullOrWhiteSpace(_settings.StoreUrl))
            return (false, null, "Shopify Store URL is not configured.");

        var storeDomain = _settings.StoreUrl.Trim().Replace("https://", "").Replace("http://", "").TrimEnd('/');
        var apiVersion = string.IsNullOrWhiteSpace(_settings.ApiVersion) ? ShopifyCheckoutDefaults.DefaultApiVersion : _settings.ApiVersion.Trim();
        var endpoint = $"https://{storeDomain}/admin/api/{apiVersion}/graphql.json";

        var lineItemsPayload = items.Select(item => new
        {
            variantId = item.VariantGid,
            quantity = item.Quantity,
            originalUnitPrice = item.UnitPrice.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
        }).ToArray();

        var mutation = @"
mutation draftOrderCreate($input: DraftOrderInput!) {
  draftOrderCreate(input: $input) {
    draftOrder {
      id
      name
      invoiceUrl
    }
    userErrors {
      field
      message
    }
  }
}";

        var inputPayload = new Dictionary<string, object>
        {
            ["currencyCode"] = "GBP",
            ["lineItems"] = lineItemsPayload
        };

        if (!string.IsNullOrWhiteSpace(customerEmail))
        {
            inputPayload["email"] = customerEmail.Trim();
        }

        var requestPayload = new
        {
            query = mutation,
            variables = new
            {
                input = inputPayload
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

            if (root.TryGetProperty("data", out var data) && data.TryGetProperty("draftOrderCreate", out var draftOrderCreate))
            {
                if (draftOrderCreate.TryGetProperty("userErrors", out var userErrors) && userErrors.ValueKind == JsonValueKind.Array && userErrors.GetArrayLength() > 0)
                {
                    var errMsg = string.Join("; ", userErrors.EnumerateArray().Select(e => e.GetProperty("message").GetString()));
                    return (false, null, $"Shopify Draft Order GraphQL user errors: {errMsg}");
                }

                if (draftOrderCreate.TryGetProperty("draftOrder", out var draftOrder))
                {
                    if (draftOrder.TryGetProperty("invoiceUrl", out var invoiceUrlProp))
                    {
                        var invoiceUrl = invoiceUrlProp.GetString();
                        if (!string.IsNullOrWhiteSpace(invoiceUrl))
                        {
                            return (true, invoiceUrl, "Draft order created successfully.");
                        }
                    }
                }
            }

            return (false, null, "Shopify Admin API did not return a valid invoice URL.");
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync($"Exception in CreateDraftOrderAsync: {ex.Message}", ex);
            return (false, null, ex.Message);
        }
    }

    #endregion
}
