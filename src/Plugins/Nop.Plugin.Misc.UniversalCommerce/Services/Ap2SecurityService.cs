using System;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Nop.Plugin.Misc.UniversalCommerce.Models;
using Microsoft.Extensions.Caching.Hybrid; // Native to modern .NET runtimes

namespace Nop.Plugin.Misc.UniversalCommerce.Services
{
    public class Ap2SecurityService : IAp2SecurityService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly HybridCache _cache;
        private const string GoogleAp2KeysUrl = "https://google.com";

        public Ap2SecurityService(IHttpClientFactory httpClientFactory, HybridCache cache)
        {
            _httpClientFactory = httpClientFactory;
            _cache = cache;
        }

        public async Task<string> GetGoogleKeysJsonAsync()
        {
            // HybridCache handles multi-thread token requests safely out of the box
            return await _cache.GetOrCreateAsync("google-ap2-public-keys", async token =>
            {
                var client = _httpClientFactory.CreateClient();
                return await client.GetStringAsync(GoogleAp2KeysUrl, token);
            },
            options: new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromHours(24) // Google keys rotate infrequently
            });
        }
        public async Task<bool> ValidateAgentMandateAsync(string rawTokenJson, decimal systemPrice, string expectedSku)
        {
            try
            {
                if (string.IsNullOrEmpty(rawTokenJson))
                    return false;

                // 1. Parse the incoming AP2 structure
                var tokenEnvelope = JsonSerializer.Deserialize<Ap2TokenPayload>(rawTokenJson);
                if (tokenEnvelope == null || string.IsNullOrEmpty(tokenEnvelope.Signature))
                    return false;

                // 2. Fetch Google's trusted signing public keys (Cache this in production)
                var client = _httpClientFactory.CreateClient();
                var keysResponse = await client.GetStringAsync(GoogleAp2KeysUrl);
                using var keysDocument = JsonDocument.Parse(keysResponse);

                // 3. Extract the inner signed message
                var signedMessageBytes = Encoding.UTF8.GetBytes(tokenEnvelope.SignedMessage);
                var signatureBytes = Convert.FromBase64String(tokenEnvelope.Signature);

                // 4. Verify signature against Google's public key list
                bool isSignatureValid = false;
                foreach (var keyNode in keysDocument.RootElement.GetProperty("keys").EnumerateArray())
                {
                    if (keyNode.GetProperty("protocolVersion").GetString() != tokenEnvelope.ProtocolVersion)
                        continue;

                    var keyValue = keyNode.GetProperty("keyValue").GetString();
                    var publicKeyBytes = Convert.FromBase64String(keyValue!);

                    using var ecdsa = ECDsa.Create();
                    ecdsa.ImportSubjectPublicKeyInfo(publicKeyBytes, out _);

                    if (ecdsa.VerifyData(signedMessageBytes, signatureBytes, HashAlgorithmName.SHA256))
                    {
                        isSignatureValid = true;
                        break;
                    }
                }

                if (!isSignatureValid)
                    return false;

                // 5. Audit validation (Prevent agent price tampering)
                var payloadDetails = JsonSerializer.Deserialize<Ap2SignedMessage>(tokenEnvelope.SignedMessage);
                if (payloadDetails == null || payloadDetails.CartDetails == null)
                    return false;

                if (payloadDetails.CartDetails.Sku != expectedSku)
                    return false;
                if (payloadDetails.CartDetails.ExpectedPrice != systemPrice)
                    return false; // Thwart mid-flight price manipulation

                return true;
            }
            catch
            {
                // In production, log specific exceptions to NopLogger
                return false;
            }
        }
    }
}