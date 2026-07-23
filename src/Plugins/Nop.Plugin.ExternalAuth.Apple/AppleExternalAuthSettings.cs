using Nop.Core.Configuration;

namespace Nop.Plugin.ExternalAuth.Apple;

/// <summary>
/// Represents settings of the Apple authentication method
/// </summary>
public class AppleExternalAuthSettings : ISettings
{
    /// <summary>
    /// Gets or sets OAuth2 client identifier (Services ID)
    /// </summary>
    public string ClientId { get; set; }

    /// <summary>
    /// Gets or sets Apple Developer Team ID
    /// </summary>
    public string TeamId { get; set; }
    
    /// <summary>
    /// Gets or sets Key ID for the Private Key
    /// </summary>
    public string KeyId { get; set; }
    
    /// <summary>
    /// Gets or sets Private Key (text content of .p8 file)
    /// </summary>
    public string PrivateKey { get; set; }
}