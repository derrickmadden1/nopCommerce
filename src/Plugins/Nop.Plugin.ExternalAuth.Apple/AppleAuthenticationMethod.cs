using Nop.Plugin.ExternalAuth.Apple.Components;
using Nop.Services.Authentication.External;
using Nop.Services.Configuration;
using Nop.Services.Helpers;
using Nop.Services.Localization;
using Nop.Services.Plugins;

namespace Nop.Plugin.ExternalAuth.Apple;

/// <summary>
/// Represents method for the authentication with Apple account
/// </summary>
public class AppleAuthenticationMethod : BasePlugin, IExternalAuthenticationMethod
{
    #region Fields

    protected readonly ILocalizationService _localizationService;
    protected readonly ISettingService _settingService;
    protected readonly IWebHelper _webHelper;

    #endregion

    #region Ctor

    public AppleAuthenticationMethod(ILocalizationService localizationService,
        ISettingService settingService,
        IWebHelper webHelper)
    {
        _localizationService = localizationService;
        _settingService = settingService;
        _webHelper = webHelper;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Gets a configuration page URL
    /// </summary>
    public override string GetConfigurationPageUrl()
    {
        return $"{_webHelper.GetStoreLocation()}Admin/AppleAuthentication/Configure";
    }

    /// <summary>
    /// Gets a type of a view component for displaying plugin in public store
    /// </summary>
    /// <returns>View component type</returns>
    public Type GetPublicViewComponent()
    {
        return typeof(AppleAuthenticationViewComponent);
    }

    /// <summary>
    /// Install the plugin
    /// </summary>
    /// <returns>A task that represents the asynchronous operation</returns>
    public override async Task InstallAsync()
    {
        //settings
        await _settingService.SaveSettingAsync(new AppleExternalAuthSettings());

        //locales
        await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
        {
            ["Plugins.ExternalAuth.Apple.AuthenticationDataDeletedSuccessfully"] = "Data deletion request completed",
            ["Plugins.ExternalAuth.Apple.AuthenticationDataExist"] = "Data deletion request is pending, please contact the admin",
            ["Plugins.ExternalAuth.Apple.ClientKeyIdentifier"] = "App ID/API Key",
            ["Plugins.ExternalAuth.Apple.ClientKeyIdentifier.Hint"] = "Enter your app ID/API key here. You can find it on your Apple application page.",
            ["Plugins.ExternalAuth.Apple.ClientSecret"] = "App Secret",
            ["Plugins.ExternalAuth.Apple.ClientSecret.Hint"] = "Enter your app secret here. You can find it on your Apple application page.",
            ["Plugins.ExternalAuth.Apple.Instructions"] = "<p>To configure authentication with Apple, please follow these steps:<br/><br/><ol><li>Navigate to the <a href=\"https://developers.Apple.com/apps\" target =\"_blank\"> Apple for Developers</a> page and sign in. If you don't already have a Apple account, use the <b>Sign up for Apple</b> link on the login page to create one.</li><li>Tap the <b>+ Add a New App button</b> in the upper right corner to create a new App ID. (If this is your first app with Apple, the text of the button will be <b>Create a New App</b>.)</li><li>Fill out the form and tap the <b>Create App ID button</b>.</li><li>The <b>Product Setup</b> page is displayed, letting you select the features for your new app. Click <b>Get Started</b> on <b>Apple Login</b>.</li><li>Click the <b>Settings</b> link in the menu at the left, you are presented with the <b>Client OAuth Settings</b> page with some defaults already set.</li><li>Enter \"{0:s}signin-Apple\" into the <b>Valid OAuth Redirect URIs</b> field.</li><li>From User data deletion dropdown menu select \"Data deletion instructions URL\" </li><li> Enter \"{0:s}Apple/data-deletion-callback/\" into the <b> User data deletion </b> input field.</li><li>Click <b>Save Changes</b>.</li><li>Click the <b>Dashboard</b> link in the left navigation.</li><li>Copy your App ID and App secret below.</li></ol><br/><br/></p>"
        });

        await base.InstallAsync();
    }

    /// <summary>
    /// Uninstall the plugin
    /// </summary>
    /// <returns>A task that represents the asynchronous operation</returns>
    public override async Task UninstallAsync()
    {
        //settings
        await _settingService.DeleteSettingAsync<AppleExternalAuthSettings>();

        //locales
        await _localizationService.DeleteLocaleResourcesAsync("Plugins.ExternalAuth.Apple");

        await base.UninstallAsync();
    }

    #endregion
}