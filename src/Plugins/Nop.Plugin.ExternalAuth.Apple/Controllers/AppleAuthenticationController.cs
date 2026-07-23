using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using AspNet.Security.OAuth.Apple;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Nop.Core;
using Nop.Core.Http;
using Nop.Plugin.ExternalAuth.Apple.Models;
using Nop.Services.Authentication.External;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.ExternalAuth.Apple.Controllers;

[AutoValidateAntiforgeryToken]
public class AppleAuthenticationController : BasePluginController
{
    #region Fields

    protected readonly AppleExternalAuthSettings _AppleExternalAuthSettings;
    protected readonly IAuthenticationPluginManager _authenticationPluginManager;
    protected readonly IExternalAuthenticationService _externalAuthenticationService;
    protected readonly ILocalizationService _localizationService;
    protected readonly INotificationService _notificationService;
    protected readonly IOptionsMonitorCache<AppleAuthenticationOptions> _optionsCache;
    protected readonly IPermissionService _permissionService;
    protected readonly ISettingService _settingService;
    protected readonly IStoreContext _storeContext;
    protected readonly IWorkContext _workContext;

    #endregion

    #region Ctor

    public AppleAuthenticationController(AppleExternalAuthSettings AppleExternalAuthSettings,
        IAuthenticationPluginManager authenticationPluginManager,
        IExternalAuthenticationService externalAuthenticationService,
        ILocalizationService localizationService,
        INotificationService notificationService,
        IOptionsMonitorCache<AppleAuthenticationOptions> optionsCache,
        IPermissionService permissionService,
        ISettingService settingService,
        IStoreContext storeContext,
        IWorkContext workContext)
    {
        _AppleExternalAuthSettings = AppleExternalAuthSettings;
        _authenticationPluginManager = authenticationPluginManager;
        _externalAuthenticationService = externalAuthenticationService;
        _localizationService = localizationService;
        _notificationService = notificationService;
        _optionsCache = optionsCache;
        _permissionService = permissionService;
        _settingService = settingService;
        _storeContext = storeContext;
        _workContext = workContext;
    }

    #endregion

    #region Methods

    [AuthorizeAdmin]
    [Area(AreaNames.ADMIN)]
    [CheckPermission(StandardPermission.Configuration.MANAGE_EXTERNAL_AUTHENTICATION_METHODS)]
    public IActionResult Configure()
    {
        var model = new ConfigurationModel
        {
            ClientId = _AppleExternalAuthSettings.ClientId,
            TeamId = _AppleExternalAuthSettings.TeamId,
            KeyId = _AppleExternalAuthSettings.KeyId,
            PrivateKey = _AppleExternalAuthSettings.PrivateKey
        };

        return View("~/Plugins/ExternalAuth.Apple/Views/Configure.cshtml", model);
    }

    [HttpPost]
    [AuthorizeAdmin]
    [Area(AreaNames.ADMIN)]
    [CheckPermission(StandardPermission.Configuration.MANAGE_EXTERNAL_AUTHENTICATION_METHODS)]
    public async Task<IActionResult> Configure(ConfigurationModel model)
    {
        if (!ModelState.IsValid)
            return Configure();

        //save settings
        _AppleExternalAuthSettings.ClientId = model.ClientId;
        _AppleExternalAuthSettings.TeamId = model.TeamId;
        _AppleExternalAuthSettings.KeyId = model.KeyId;
        _AppleExternalAuthSettings.PrivateKey = model.PrivateKey;
        await _settingService.SaveSettingAsync(_AppleExternalAuthSettings);

        //clear Apple authentication options cache
        _optionsCache.TryRemove("Apple");

        _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));

        return Configure();
    }

    public async Task<IActionResult> Login(string returnUrl)
    {
        var store = await _storeContext.GetCurrentStoreAsync();
        var methodIsAvailable = await _authenticationPluginManager
            .IsPluginActiveAsync(AppleAuthenticationDefaults.SystemName, await _workContext.GetCurrentCustomerAsync(), store.Id);
        if (!methodIsAvailable)
            throw new NopException("Apple authentication module cannot be loaded");

        if (string.IsNullOrEmpty(_AppleExternalAuthSettings.ClientId) ||
            string.IsNullOrEmpty(_AppleExternalAuthSettings.TeamId) ||
            string.IsNullOrEmpty(_AppleExternalAuthSettings.KeyId) ||
            string.IsNullOrEmpty(_AppleExternalAuthSettings.PrivateKey))
            throw new NopException("Apple authentication module not configured");

        //configure login callback action
        var authenticationProperties = new AuthenticationProperties
        {
            RedirectUri = Url.Action("LoginCallback", "AppleAuthentication", new { returnUrl = returnUrl })
        };
        authenticationProperties.SetString(AppleAuthenticationDefaults.ErrorCallback, Url.RouteUrl(NopRouteNames.General.LOGIN, new { returnUrl }));

        return Challenge(authenticationProperties, "Apple");
    }

    public async Task<IActionResult> LoginCallback(string returnUrl)
    {
        //authenticate Apple user
        var authenticateResult = await HttpContext.AuthenticateAsync("Apple");
        if (!authenticateResult.Succeeded || !authenticateResult.Principal.Claims.Any())
            return RedirectToRoute(NopRouteNames.General.LOGIN);

        //create external authentication parameters
        var authenticationParameters = new ExternalAuthenticationParameters
        {
            ProviderSystemName = AppleAuthenticationDefaults.SystemName,
            AccessToken = await HttpContext.GetTokenAsync("Apple", "access_token"),
            Email = authenticateResult.Principal.FindFirst(claim => claim.Type == ClaimTypes.Email)?.Value,
            ExternalIdentifier = authenticateResult.Principal.FindFirst(claim => claim.Type == ClaimTypes.NameIdentifier)?.Value,
            ExternalDisplayIdentifier = authenticateResult.Principal.FindFirst(claim => claim.Type == ClaimTypes.Name)?.Value,
            Claims = authenticateResult.Principal.Claims.Select(claim => new ExternalAuthenticationClaim(claim.Type, claim.Value)).ToList()
        };

        //authenticate Nop user
        return await _externalAuthenticationService.AuthenticateAsync(authenticationParameters, returnUrl);
    }

    public async Task<IActionResult> DataDeletionStatusCheck(int earId)
    {
        var externalAuthenticationRecord = await _externalAuthenticationService.GetExternalAuthenticationRecordByIdAsync(earId);
        if (externalAuthenticationRecord is not null)
            _notificationService.WarningNotification(await _localizationService.GetResourceAsync("Plugins.ExternalAuth.Apple.AuthenticationDataExist"));
        else
            _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Plugins.ExternalAuth.Apple.AuthenticationDataDeletedSuccessfully"));

        return RedirectToRoute(NopRouteNames.General.CUSTOMER_INFO);
    }

    #endregion
}