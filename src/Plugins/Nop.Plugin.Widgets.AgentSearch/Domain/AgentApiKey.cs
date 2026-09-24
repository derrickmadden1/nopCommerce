using System;
using Nop.Core;

namespace Nop.Plugin.Widgets.AgentSearch.Domain
{
    /// <summary>
    /// Represents a multi-tenant API key issued to a third-party AI agent or platform.
    /// </summary>
    public class AgentApiKey : BaseEntity
    {
        /// <summary>
        /// Human-readable name or identifier for the agent (e.g., "OpenAI-ChatGPT", "Perplexity-Bot").
        /// </summary>
        public string AgentName { get; set; } = string.Empty;

        /// <summary>
        /// SHA-256 hash of the issued raw API secret key.
        /// </summary>
        public string KeyHash { get; set; } = string.Empty;

        /// <summary>
        /// Key prefix for safe display and logging (e.g., "ak_live_a1b2").
        /// </summary>
        public string KeyPrefix { get; set; } = string.Empty;

        /// <summary>
        /// Master toggle indicating if the API key is active.
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Maximum requests allowed per minute for this specific key (0 = unlimited).
        /// </summary>
        public int RateLimitPerMinute { get; set; } = 600;

        /// <summary>
        /// Comma-separated list of allowed scopes (e.g., "Search.Read,Catalog.Read").
        /// </summary>
        public string AllowedScopes { get; set; } = "Search.Read,Catalog.Read";

        /// <summary>
        /// UTC timestamp when the key was generated.
        /// </summary>
        public DateTime CreatedOnUtc { get; set; }

        /// <summary>
        /// UTC timestamp when the key was last presented for authentication.
        /// </summary>
        public DateTime? LastUsedOnUtc { get; set; }

        /// <summary>
        /// Optional expiration UTC timestamp. Null means no expiration.
        /// </summary>
        public DateTime? ExpiresOnUtc { get; set; }
    }
}
