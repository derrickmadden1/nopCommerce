using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Nop.Plugin.Widgets.ShopifyCheckout.Services;

/// <summary>
/// Represents the Shopify Storefront API service
/// </summary>
public class ShopifyStorefrontService : IShopifyStorefrontService
{
    #region Fields

    private readonly HttpClient _httpClient;
    private readonly ShopifyCheckoutSettings _settings;
    private readonly IShopifyAdminApiService _adminApiService;
    private readonly ILogger<ShopifyStorefrontService> _logger;

    #endregion

    #region Ctor

    public ShopifyStorefrontService(
        HttpClient httpClient,
        ShopifyCheckoutSettings settings,
        IShopifyAdminApiService adminApiService,
        ILogger<ShopifyStorefrontService> logger)
    {
        _httpClient = httpClient;
        _settings = settings;
        _adminApiService = adminApiService;
        _logger = logger;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Creates a Shopify cart via Storefront GraphQL API and returns the checkout URL
    /// </summary>
    /// <param name="lineItems">List of line items containing merchandiseId (variant GID) and quantity</param>
    /// <returns>Checkout URL if successful, error messages otherwise</returns>
    public async Task<(string CheckoutUrl, List<string> Errors)> CreateCartAsync(IEnumerable<(string MerchandiseId, int Quantity)> lineItems)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(_settings.StoreUrl))
        {
            errors.Add("Shopify Store URL is not configured.");
            return (null, errors);
        }

        if (string.IsNullOrWhiteSpace(_settings.StorefrontAccessToken))
        {
            var (success, token, msg) = await _adminApiService.GetOrCreateStorefrontAccessTokenAsync();
            if (success && !string.IsNullOrWhiteSpace(token))
            {
                _settings.StorefrontAccessToken = token;
            }
            else
            {
                errors.Add($"Shopify Storefront Access Token is not configured ({msg}).");
                return (null, errors);
            }
        }

        var storeDomain = _settings.StoreUrl.Trim().Replace("https://", "").Replace("http://", "").TrimEnd('/');
        var apiVersion = string.IsNullOrWhiteSpace(_settings.ApiVersion) ? ShopifyCheckoutDefaults.DefaultApiVersion : _settings.ApiVersion.Trim();
        var endpoint = $"https://{storeDomain}/api/{apiVersion}/graphql.json";

        var linesPayload = lineItems.Select(item => new
        {
            merchandiseId = item.MerchandiseId,
            quantity = item.Quantity
        }).ToArray();

        if (!linesPayload.Any())
        {
            errors.Add("No valid items to checkout.");
            return (null, errors);
        }

        var mutation = @"
mutation cartCreate($input: CartInput!) {
  cartCreate(input: $input) {
    cart {
      id
      checkoutUrl
    }
    userErrors {
      field
      message
    }
  }
}";

        var requestBody = new
        {
            query = mutation,
            variables = new
            {
                input = new
                {
                    lines = linesPayload
                }
            }
        };

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.Add("X-Shopify-Storefront-Access-Token", _settings.StorefrontAccessToken.Trim());
            request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Shopify Storefront API returned HTTP {StatusCode}: {Content}", response.StatusCode, responseContent);
                errors.Add($"Shopify Storefront API error ({response.StatusCode}).");
                return (null, errors);
            }

            using var doc = JsonDocument.Parse(responseContent);
            var root = doc.RootElement;

            // Check top-level GraphQL errors
            if (root.TryGetProperty("errors", out var topLevelErrors) && topLevelErrors.ValueKind == JsonValueKind.Array)
            {
                foreach (var err in topLevelErrors.EnumerateArray())
                {
                    if (err.TryGetProperty("message", out var msg))
                    {
                        errors.Add(msg.GetString());
                    }
                }
                return (null, errors);
            }

            // Check cartCreate data
            if (root.TryGetProperty("data", out var data) && data.TryGetProperty("cartCreate", out var cartCreate))
            {
                // Check userErrors inside cartCreate
                if (cartCreate.TryGetProperty("userErrors", out var userErrors) && userErrors.ValueKind == JsonValueKind.Array)
                {
                    foreach (var uErr in userErrors.EnumerateArray())
                    {
                        if (uErr.TryGetProperty("message", out var uMsg))
                        {
                            errors.Add(uMsg.GetString());
                        }
                    }
                }

                if (errors.Any())
                    return (null, errors);

                // Get cart.checkoutUrl
                if (cartCreate.TryGetProperty("cart", out var cart) && cart.TryGetProperty("checkoutUrl", out var checkoutUrlProp))
                {
                    var checkoutUrl = checkoutUrlProp.GetString();
                    if (!string.IsNullOrWhiteSpace(checkoutUrl))
                    {
                        return (checkoutUrl, errors);
                    }
                }
            }

            errors.Add("Shopify did not return a valid checkout URL.");
            return (null, errors);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to call Shopify Storefront GraphQL API");
            errors.Add($"Failed to communicate with Shopify: {ex.Message}");
            return (null, errors);
        }
    }

    #endregion
}
