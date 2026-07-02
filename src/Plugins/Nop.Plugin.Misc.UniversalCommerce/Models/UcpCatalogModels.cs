#pragma warning disable CS8618
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Nop.Plugin.Misc.UniversalCommerce.Models
{
    public class UcpCatalogResponse
    {
        [JsonPropertyName("total_items")]
        public int TotalItems { get; set; }

        [JsonPropertyName("page_index")]
        public int PageIndex { get; set; }

        [JsonPropertyName("has_next_page")]
        public bool HasNextPage { get; set; }

        [JsonPropertyName("products")]
        public List<UcpCatalogItem> Products { get; set; }
    }

    public class UcpCatalogItem
    {
        [JsonPropertyName("sku")]
        public string Sku { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("price")]
        public decimal Price { get; set; }

        [JsonPropertyName("currency")]
        public string Currency { get; set; } = "GBP";

        [JsonPropertyName("in_stock")]
        public bool InStock { get; set; }

        [JsonPropertyName("url_slug")]
        public string UrlSlug { get; set; }
    }
}
