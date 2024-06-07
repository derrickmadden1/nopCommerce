using Nop.Core;
using Nop.Core.Configuration;
using Nop.Core.Domain.Cms;
using Nop.Core.Infrastructure;
using Nop.Services.Cms;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Plugins;
using Nop.Services.Security;

namespace Nop.Plugin.Misc.Acl;

/// <summary>
/// Represents plugin to enable the access control list feature
/// </summary>
public class AclPlugin : BasePlugin, IMiscPlugin
{
    #region Fields

    protected readonly AppSettings _appSettings;
    protected readonly ILanguageService _languageService;
    protected readonly ILocalizationService _localizationService;
    protected readonly INopFileProvider _fileProvider;
    protected readonly ISettingService _settingService;
    protected readonly IWebHelper _webHelper;
    protected readonly PermissionService _permissionService;
    protected readonly WidgetSettings _widgetSettings;

    #endregion

    #region Ctor

    public AclPlugin(AppSettings appSettings,
        ILanguageService languageService,
        ILocalizationService localizationService,
        INopFileProvider fileProvider,
        ISettingService settingService,
        IWebHelper webHelper,
        PermissionService permissionService,
        WidgetSettings widgetSettings)
    {
        _appSettings = appSettings;
        _languageService = languageService;
        _localizationService = localizationService;
        _fileProvider = fileProvider;
        _settingService = settingService;
        _webHelper = webHelper;
        _permissionService = permissionService;
        _widgetSettings = widgetSettings;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Gets a configuration page URL
    /// </summary>
    public override string GetConfigurationPageUrl()
    {
        return $"{_webHelper.GetStoreLocation()}Admin/Acl/Configure";
    }

    /// <summary>
    /// Install plugin
    /// </summary>
    /// <returns>A task that represents the asynchronous operation</returns>
    public override async Task InstallAsync()
    {
        //locales
        await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
        {
            
        });
    }
    
    #endregion

    #region Properties

    /// <summary>
    /// Gets a value indicating whether to hide this plugin on the widget list page in the admin area
    /// </summary>
    public bool HideInWidgetList => true;

    #endregion
}