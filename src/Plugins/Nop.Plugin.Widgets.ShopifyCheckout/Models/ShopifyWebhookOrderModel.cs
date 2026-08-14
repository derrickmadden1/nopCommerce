using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Nop.Web.Framework.Models;

namespace Nop.Plugin.Widgets.ShopifyCheckout.Models;

public record ShopifyWebhookOrderModel : BaseNopModel
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("email")]
    public string Email { get; set; }

    [JsonPropertyName("total_price")]
    public string TotalPrice { get; set; }

    [JsonPropertyName("currency")]
    public string Currency { get; set; }

    [JsonPropertyName("financial_status")]
    public string FinancialStatus { get; set; }

    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("line_items")]
    public List<ShopifyWebhookLineItemModel> LineItems { get; set; } = new();

    [JsonPropertyName("customer")]
    public ShopifyWebhookCustomerModel Customer { get; set; }
}

public record ShopifyWebhookLineItemModel : BaseNopModel
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("variant_id")]
    public long? VariantId { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; }

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }

    [JsonPropertyName("sku")]
    public string Sku { get; set; }

    [JsonPropertyName("price")]
    public string Price { get; set; }
}

public record ShopifyWebhookCustomerModel : BaseNopModel
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("email")]
    public string Email { get; set; }

    [JsonPropertyName("first_name")]
    public string FirstName { get; set; }

    [JsonPropertyName("last_name")]
    public string LastName { get; set; }
}
