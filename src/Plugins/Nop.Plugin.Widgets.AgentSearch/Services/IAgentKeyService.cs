using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nop.Plugin.Widgets.AgentSearch.Domain;

namespace Nop.Plugin.Widgets.AgentSearch.Services
{
    public interface IAgentKeyService
    {
        /// <summary>
        /// Generates a new cryptographically random Agent API key and stores its SHA-256 hash.
        /// Returns the entity and the plain-text raw key (returned only once at creation).
        /// </summary>
        Task<(AgentApiKey KeyEntity, string RawKey)> CreateKeyAsync(
            string agentName,
            int rateLimitPerMinute = 600,
            string allowedScopes = "Search.Read,Catalog.Read",
            DateTime? expiresOnUtc = null);

        /// <summary>
        /// Validates a raw API key against stored active key hashes, checking expiration and required scope.
        /// Updates LastUsedOnUtc if valid.
        /// </summary>
        Task<AgentApiKey?> ValidateKeyAsync(string rawKey, string? requiredScope = null);

        /// <summary>
        /// Retrieves all API key entities for admin management.
        /// </summary>
        Task<IList<AgentApiKey>> GetAllKeysAsync();

        /// <summary>
        /// Retrieves a specific key entity by ID.
        /// </summary>
        Task<AgentApiKey?> GetKeyByIdAsync(int id);

        /// <summary>
        /// Toggles active status (activates or deactivates key).
        /// </summary>
        Task ToggleKeyStatusAsync(int id);

        /// <summary>
        /// Deletes an API key entity.
        /// </summary>
        Task DeleteKeyAsync(int id);

        /// <summary>
        /// Computes SHA-256 hash of a raw key string.
        /// </summary>
        string HashKey(string rawKey);
    }
}
