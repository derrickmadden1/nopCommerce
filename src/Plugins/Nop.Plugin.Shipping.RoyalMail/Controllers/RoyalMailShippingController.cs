using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Plugin.Shipping.RoyalMail.Models;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Shipping.RoyalMail.Controllers;

[AuthorizeAdmin]
[Area(AreaNames.ADMIN)]
[AutoValidateAntiforgeryToken]
public class RoyalMailShippingController : BasePluginController
{
    #region Fields

    private readonly ILocalizationService _localizationService;
    private readonly INotificationService _notificationService;
    private readonly IPermissionService _permissionService;
    private readonly ISettingService _settingService;
    private readonly IStoreContext _storeContext;

    #endregion

    #region Ctor

    public RoyalMailShippingController(ILocalizationService localizationService,
        INotificationService notificationService,
        IPermissionService permissionService,
        ISettingService settingService,
        IStoreContext storeContext)
    {
        _localizationService = localizationService;
        _notificationService = notificationService;
        _permissionService = permissionService;
        _settingService = settingService;
        _storeContext = storeContext;
    }

    #endregion

    #region Methods

    [CheckPermission(StandardPermission.Configuration.MANAGE_SHIPPING_SETTINGS)]
    public async Task<IActionResult> Configure()
    {
        var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
        var royalMailSettings = await _settingService.LoadSettingAsync<RoyalMailSettings>(storeScope);

        var model = new ConfigurationModel
        {
            UseSandbox = royalMailSettings.UseSandbox,
            ClientId = royalMailSettings.ClientId,
            ClientSecret = royalMailSettings.ClientSecret,
            UseWebTrackingUrlFallback = royalMailSettings.UseWebTrackingUrlFallback,
            ActiveStoreScopeConfiguration = storeScope
        };

        if (storeScope > 0)
        {
            model.UseSandbox_OverrideForStore = await _settingService.SettingExistsAsync(royalMailSettings, x => x.UseSandbox, storeScope);
            model.ClientId_OverrideForStore = await _settingService.SettingExistsAsync(royalMailSettings, x => x.ClientId, storeScope);
            model.ClientSecret_OverrideForStore = await _settingService.SettingExistsAsync(royalMailSettings, x => x.ClientSecret, storeScope);
            model.UseWebTrackingUrlFallback_OverrideForStore = await _settingService.SettingExistsAsync(royalMailSettings, x => x.UseWebTrackingUrlFallback, storeScope);
        }

        return View("~/Plugins/Shipping.RoyalMail/Views/Configure.cshtml", model);
    }

    [HttpPost]
    [CheckPermission(StandardPermission.Configuration.MANAGE_SHIPPING_SETTINGS)]
    public async Task<IActionResult> Configure(ConfigurationModel model)
    {
        if (!ModelState.IsValid)
            return await Configure();

        var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
        var royalMailSettings = await _settingService.LoadSettingAsync<RoyalMailSettings>(storeScope);

        royalMailSettings.UseSandbox = model.UseSandbox;
        royalMailSettings.ClientId = model.ClientId;
        royalMailSettings.ClientSecret = model.ClientSecret;
        royalMailSettings.UseWebTrackingUrlFallback = model.UseWebTrackingUrlFallback;

        await _settingService.SaveSettingOverridablePerStoreAsync(royalMailSettings, x => x.UseSandbox, model.UseSandbox_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(royalMailSettings, x => x.ClientId, model.ClientId_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(royalMailSettings, x => x.ClientSecret, model.ClientSecret_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(royalMailSettings, x => x.UseWebTrackingUrlFallback, model.UseWebTrackingUrlFallback_OverrideForStore, storeScope, false);

        await _settingService.ClearCacheAsync();

        _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));

        return await Configure();
    }

    #endregion
}
