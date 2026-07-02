#pragma warning disable CS8618
using System.Text.Json.Serialization;

namespace Nop.Plugin.Misc.UniversalCommerce.Models
{
    public class Ap2TokenPayload
    {
        [JsonPropertyName("signature")]
        public string Signature { get; set; }

        [JsonPropertyName("protocolVersion")]
        public string ProtocolVersion { get; set; } // ECv2 or AP2-v1

        [JsonPropertyName("signedMessage")]
        public string SignedMessage { get; set; } // JSON string containing Mandate and Cart
    }

    public class Ap2SignedMessage
    {
        [JsonPropertyName("mandateId")]
        public string MandateId { get; set; }

        [JsonPropertyName("intentEnvelope")]
        public string IntentEnvelope { get; set; }

        [JsonPropertyName("cartDetails")]
        public Ap2CartDetails CartDetails { get; set; }
    }

    public class Ap2CartDetails
    {
        [JsonPropertyName("sku")]
        public string Sku { get; set; }

        [JsonPropertyName("expectedPrice")]
        public decimal ExpectedPrice { get; set; }
    }
}
