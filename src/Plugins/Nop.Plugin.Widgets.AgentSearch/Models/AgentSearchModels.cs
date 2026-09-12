using System.Collections.Generic;

namespace Nop.Plugin.Widgets.AgentSearch.Models
{
    public class AgentSearchRequest
    {
        public string? Query { get; set; }
        public int? MaxResults { get; set; }
        public AgentSearchFilters? Filters { get; set; }
    }

    public class AgentSearchFilters
    {
        public bool? InStock { get; set; }
        public decimal? MaxPrice { get; set; }
        public decimal? MinPrice { get; set; }
        public string? ShipsTo { get; set; } // ISO country code, e.g. "GB"
        public string? Category { get; set; }
    }

    public class AgentSearchResponse
    {
        public List<AgentProductResult> Results { get; set; } = new();
        public string? QueryInterpreted { get; set; }
        public int Total { get; set; }
    }

    public class AgentProductResult
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public AgentPrice Price { get; set; } = new();
        public string Availability { get; set; } = "in_stock";
        public string? Url { get; set; }
        public string? Image { get; set; }
        public List<string> CategoryNames { get; set; } = new();
        public List<string> ManufacturerNames { get; set; } = new();
        public List<string> ShipsTo { get; set; } = new();
        public Dictionary<string, List<string>> Attributes { get; set; } = new();
    }

    public class AgentPrice
    {
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "GBP";
    }

    public class AgentSearchErrorResponse
    {
        public string Error { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
    }
}
