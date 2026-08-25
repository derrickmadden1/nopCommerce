using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Nop.Web.Framework.Models;

namespace Nop.Plugin.Widgets.ShopifyCheckout.Models;

public record ShopifyWebhookOrderModel : BaseNopModel
{
    [JsonProperty("id")]
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonProperty("name")]
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonProperty("email")]
    [JsonPropertyName("email")]
    public string Email { get; set; }

    [JsonProperty("total_price")]
    [JsonPropertyName("total_price")]
    public string TotalPrice { get; set; }

    [JsonProperty("currency")]
    [JsonPropertyName("currency")]
    public string Currency { get; set; }

    [JsonProperty("financial_status")]
    [JsonPropertyName("financial_status")]
    public string FinancialStatus { get; set; }

    [JsonProperty("created_at")]
    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonProperty("line_items")]
    [JsonPropertyName("line_items")]
    public List<ShopifyWebhookLineItemModel> LineItems { get; set; } = new();

    [JsonProperty("customer")]
    [JsonPropertyName("customer")]
    public ShopifyWebhookCustomerModel Customer { get; set; }

    [JsonProperty("note_attributes")]
    [JsonPropertyName("note_attributes")]
    public List<ShopifyWebhookNoteAttributeModel> NoteAttributes { get; set; } = new();

    [JsonProperty("custom_attributes")]
    [JsonPropertyName("custom_attributes")]
    public List<ShopifyWebhookNoteAttributeModel> CustomAttributes { get; set; } = new();
}

public record ShopifyWebhookNoteAttributeModel : BaseNopModel
{
    [JsonProperty("name")]
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonProperty("key")]
    [JsonPropertyName("key")]
    public string Key { get; set; }

    [JsonProperty("value")]
    [JsonPropertyName("value")]
    public string Value { get; set; }
}

public record ShopifyWebhookLineItemModel : BaseNopModel
{
    [JsonProperty("id")]
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonProperty("variant_id")]
    [JsonPropertyName("variant_id")]
    public long? VariantId { get; set; }

    [JsonProperty("title")]
    [JsonPropertyName("title")]
    public string Title { get; set; }

    [JsonProperty("quantity")]
    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }

    [JsonProperty("sku")]
    [JsonPropertyName("sku")]
    public string Sku { get; set; }

    [JsonProperty("price")]
    [JsonPropertyName("price")]
    public string Price { get; set; }
}

public record ShopifyWebhookCustomerModel : BaseNopModel
{
    [JsonProperty("id")]
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonProperty("email")]
    [JsonPropertyName("email")]
    public string Email { get; set; }

    [JsonProperty("first_name")]
    [JsonPropertyName("first_name")]
    public string FirstName { get; set; }

    [JsonProperty("last_name")]
    [JsonPropertyName("last_name")]
    public string LastName { get; set; }
}
