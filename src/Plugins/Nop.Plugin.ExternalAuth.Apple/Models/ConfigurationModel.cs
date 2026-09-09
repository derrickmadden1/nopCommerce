using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.ExternalAuth.Apple.Models;

public record ConfigurationModel : BaseNopModel
{
    [NopResourceDisplayName("Plugins.ExternalAuth.Apple.ClientKeyIdentifier")]
    public string ClientId { get; set; }

    [NopResourceDisplayName("Plugins.ExternalAuth.Apple.TeamId")]
    public string TeamId { get; set; }
    
    [NopResourceDisplayName("Plugins.ExternalAuth.Apple.KeyId")]
    public string KeyId { get; set; }
    
    [NopResourceDisplayName("Plugins.ExternalAuth.Apple.PrivateKey")]
    public string PrivateKey { get; set; }
}