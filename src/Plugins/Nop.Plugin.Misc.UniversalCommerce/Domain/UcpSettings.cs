using Nop.Core.Configuration;

namespace Nop.Plugin.Misc.UniversalCommerce.Domain
{
    public class UcpSettings : ISettings
    {
        public bool Enabled { get; set; } = true;
        public string ProtocolVersion { get; set; } = "1.0";
        public bool AllowAutonomousCheckout { get; set; } = true;
        public int PermitLimit { get; set; } = 100;
        public int WindowInSeconds { get; set; } = 60;
        public int QueueLimit { get; set; } = 10;
    }
}
