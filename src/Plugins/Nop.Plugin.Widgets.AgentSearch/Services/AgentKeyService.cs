using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Nop.Data;
using Nop.Plugin.Widgets.AgentSearch.Domain;

namespace Nop.Plugin.Widgets.AgentSearch.Services
{
    public class AgentKeyService : IAgentKeyService
    {
        private readonly IRepository<AgentApiKey> _keyRepository;

        public AgentKeyService(IRepository<AgentApiKey> keyRepository)
        {
            _keyRepository = keyRepository;
        }

        public async Task<(AgentApiKey KeyEntity, string RawKey)> CreateKeyAsync(
            string agentName,
            int rateLimitPerMinute = 600,
            string allowedScopes = "Search.Read,Catalog.Read",
            DateTime? expiresOnUtc = null)
        {
            if (string.IsNullOrWhiteSpace(agentName))
                throw new ArgumentException("Agent name is required.", nameof(agentName));

            // Generate secure random key: ak_live_<32_random_hex_chars>
            var randomBytes = RandomNumberGenerator.GetBytes(16);
            var randomHex = Convert.ToHexStringLower(randomBytes);
            var rawKey = $"ak_live_{randomHex}";
            var keyPrefix = rawKey[..14]; // "ak_live_a1b2c3d"
            var keyHash = HashKey(rawKey);

            var keyEntity = new AgentApiKey
            {
                AgentName = agentName.Trim(),
                KeyPrefix = keyPrefix,
                KeyHash = keyHash,
                IsActive = true,
                RateLimitPerMinute = rateLimitPerMinute,
                AllowedScopes = string.IsNullOrWhiteSpace(allowedScopes) ? "Search.Read" : allowedScopes.Trim(),
                CreatedOnUtc = DateTime.UtcNow,
                ExpiresOnUtc = expiresOnUtc
            };

            await _keyRepository.InsertAsync(keyEntity);
            return (keyEntity, rawKey);
        }

        public async Task<AgentApiKey?> ValidateKeyAsync(string rawKey, string? requiredScope = null)
        {
            if (string.IsNullOrWhiteSpace(rawKey))
                return null;

            var keyHash = HashKey(rawKey.Trim());
            var query = _keyRepository.Table.Where(k => k.KeyHash == keyHash && k.IsActive);
            var keyEntity = query.FirstOrDefault();

            if (keyEntity == null)
                return null;

            // Check expiration
            if (keyEntity.ExpiresOnUtc.HasValue && keyEntity.ExpiresOnUtc.Value < DateTime.UtcNow)
                return null;

            // Check required scope
            if (!string.IsNullOrWhiteSpace(requiredScope))
            {
                var scopes = keyEntity.AllowedScopes
                    .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .ToList();

                if (!scopes.Contains(requiredScope, StringComparer.OrdinalIgnoreCase) &&
                    !scopes.Contains("*"))
                {
                    return null;
                }
            }

            // Touch LastUsedOnUtc
            keyEntity.LastUsedOnUtc = DateTime.UtcNow;
            await _keyRepository.UpdateAsync(keyEntity);

            return keyEntity;
        }

        public async Task<IList<AgentApiKey>> GetAllKeysAsync()
        {
            var keys = await _keyRepository.GetAllAsync(query => query.OrderByDescending(k => k.CreatedOnUtc));
            return keys;
        }

        public async Task<AgentApiKey?> GetKeyByIdAsync(int id)
        {
            return await _keyRepository.GetByIdAsync(id);
        }

        public async Task ToggleKeyStatusAsync(int id)
        {
            var keyEntity = await _keyRepository.GetByIdAsync(id);
            if (keyEntity != null)
            {
                keyEntity.IsActive = !keyEntity.IsActive;
                await _keyRepository.UpdateAsync(keyEntity);
            }
        }

        public async Task DeleteKeyAsync(int id)
        {
            var keyEntity = await _keyRepository.GetByIdAsync(id);
            if (keyEntity != null)
            {
                await _keyRepository.DeleteAsync(keyEntity);
            }
        }

        public string HashKey(string rawKey)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(rawKey);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToHexStringLower(hash);
        }
    }
}
