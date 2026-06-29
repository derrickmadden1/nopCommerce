using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.Misc.UniversalCommerce.Models
{
    public record ConfigurationModel : BaseNopModel
    {
        [NopResourceDisplayName("Plugins.Misc.UniversalCommerce.Fields.Enabled")]
        public bool Enabled { get; set; }

        [NopResourceDisplayName("Plugins.Misc.UniversalCommerce.Fields.ProtocolVersion")]
        public string ProtocolVersion { get; set; }

        [NopResourceDisplayName("Plugins.Misc.UniversalCommerce.Fields.AllowAutonomousCheckout")]
        public bool AllowAutonomousCheckout { get; set; }

        [NopResourceDisplayName("Plugins.Misc.UniversalCommerce.Fields.PermitLimit")]
        public int PermitLimit { get; set; }

        [NopResourceDisplayName("Plugins.Misc.UniversalCommerce.Fields.WindowInSeconds")]
        public int WindowInSeconds { get; set; }

        [NopResourceDisplayName("Plugins.Misc.UniversalCommerce.Fields.QueueLimit")]
        public int QueueLimit { get; set; }
    }
}
