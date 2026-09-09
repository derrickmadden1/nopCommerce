namespace Nop.Plugin.ExternalAuth.Apple;

/// <summary>
/// Represents plugin constants
/// </summary>
public class AppleAuthenticationDefaults
{
    /// <summary>
    /// Gets a plugin system name
    /// </summary>
    public static string SystemName => "ExternalAuth.Apple";

    /// <summary>
    /// Gets a name of the route to the data deletion callback
    /// </summary>
    public static string DataDeletionCallbackRoute => "Plugin.ExternalAuth.Apple.DataDeletionCallback";

    /// <summary>
    /// Gets a name of the route to the data deletion status check
    /// </summary>
    public static string DataDeletionStatusCheckRoute => "Plugin.ExternalAuth.Apple.DataDeletionStatusCheck";

    /// <summary>
    /// Gets a name of error callback method
    /// </summary>
    public static string ErrorCallback => "ErrorCallback";
}