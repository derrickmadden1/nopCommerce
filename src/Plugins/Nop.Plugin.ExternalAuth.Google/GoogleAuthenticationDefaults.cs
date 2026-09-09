namespace Nop.Plugin.ExternalAuth.Google;

/// <summary>
/// Represents plugin constants
/// </summary>
public class GoogleAuthenticationDefaults
{
    /// <summary>
    /// Gets a plugin system name
    /// </summary>
    public static string SystemName => "ExternalAuth.Google";

    /// <summary>
    /// Gets a name of the route to the data deletion callback
    /// </summary>
    public static string DataDeletionCallbackRoute => "Plugin.ExternalAuth.Google.DataDeletionCallback";

    /// <summary>
    /// Gets a name of the route to the data deletion status check
    /// </summary>
    public static string DataDeletionStatusCheckRoute => "Plugin.ExternalAuth.Google.DataDeletionStatusCheck";

    /// <summary>
    /// Gets a name of error callback method
    /// </summary>
    public static string ErrorCallback => "ErrorCallback";
}