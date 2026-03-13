using Microsoft.AspNetCore.Mvc;
using Nop.Plugin.Widgets.MarketLocator.Domain;
using Nop.Plugin.Widgets.MarketLocator.Models;
using Nop.Plugin.Widgets.MarketLocator.Services;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Widgets.MarketLocator.Controllers;

[AuthorizeAdmin]
[Area(AreaNames.ADMIN)]
[AutoValidateAntiforgeryToken]
public class MarketLocatorAdminController : BasePluginController
{
    private readonly IMarketLocationService _locationService;
    private readonly ISettingService _settingService;
    private readonly INotificationService _notificationService;
    private readonly ILocalizationService _localizationService;
    private readonly IPermissionService _permissionService;

    public MarketLocatorAdminController(
        IMarketLocationService locationService,
        ISettingService settingService,
        INotificationService notificationService,
        ILocalizationService localizationService,
        IPermissionService permissionService)
    {
        _locationService = locationService;
        _settingService = settingService;
        _notificationService = notificationService;
        _localizationService = localizationService;
        _permissionService = permissionService;
    }

    // ── Settings ─────────────────────────────────────────────────────────────

    public async Task<IActionResult> Configure()
    {
        if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManagePlugins))
            return AccessDeniedView();

        var settings = await _settingService.LoadSettingAsync<MarketLocatorSettings>();
        var model = new MarketLocatorSettingsModel
        {
            AzureMapsKey = settings.AzureMapsKey,
            DefaultZoom = settings.DefaultZoom,
            DefaultLatitude = settings.DefaultLatitude,
            DefaultLongitude = settings.DefaultLongitude,
            ShowTeaserWidget = settings.ShowTeaserWidget,
            TeaserMaxItems = settings.TeaserMaxItems,
        };

        return View("~/Plugins/Widgets.MarketLocator/Views/Admin/Configure.cshtml", model);
    }

    [HttpPost]
    public async Task<IActionResult> Configure(MarketLocatorSettingsModel model)
    {
        if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManagePlugins))
            return AccessDeniedView();

        if (!ModelState.IsValid)
            return View("~/Plugins/Widgets.MarketLocator/Views/Admin/Configure.cshtml", model);

        var settings = await _settingService.LoadSettingAsync<MarketLocatorSettings>();
        settings.AzureMapsKey = model.AzureMapsKey;
        settings.DefaultZoom = model.DefaultZoom;
        settings.DefaultLatitude = model.DefaultLatitude;
        settings.DefaultLongitude = model.DefaultLongitude;
        settings.ShowTeaserWidget = model.ShowTeaserWidget;
        settings.TeaserMaxItems = model.TeaserMaxItems;

        await _settingService.SaveSettingAsync(settings);

        _notificationService.SuccessNotification(
            await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));

        return RedirectToAction(nameof(Configure));
    }

    // ── Location List ─────────────────────────────────────────────────────────

    public async Task<IActionResult> List()
    {
        if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManagePlugins))
            return AccessDeniedView();

        var locations = await _locationService.GetAllAsync(showUnpublished: true);
        var model = new MarketLocationListModel();
        model.Data = locations.Select(MapToModel);
        model.Total = locations.TotalCount;

        return View("~/Plugins/Widgets.MarketLocator/Views/Admin/List.cshtml", model);
    }

    // ── Create ────────────────────────────────────────────────────────────────

    public IActionResult Create()
    {
        return View("~/Plugins/Widgets.MarketLocator/Views/Admin/CreateOrEdit.cshtml",
            new MarketLocationModel());
    }

    [HttpPost]
    public async Task<IActionResult> Create(MarketLocationModel model)
    {
        if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManagePlugins))
            return AccessDeniedView();

        if (!ModelState.IsValid)
            return View("~/Plugins/Widgets.MarketLocator/Views/Admin/CreateOrEdit.cshtml", model);

        var entity = MapToEntity(model, new MarketLocation());
        await _locationService.InsertAsync(entity);

        _notificationService.SuccessNotification("Market location created.");
        return RedirectToAction(nameof(List));
    }

    // ── Edit ──────────────────────────────────────────────────────────────────

    public async Task<IActionResult> Edit(int id)
    {
        var location = await _locationService.GetByIdAsync(id);
        if (location is null) return NotFound();

        return View("~/Plugins/Widgets.MarketLocator/Views/Admin/CreateOrEdit.cshtml",
            MapToModel(location));
    }

    [HttpPost]
    public async Task<IActionResult> Edit(MarketLocationModel model)
    {
        if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManagePlugins))
            return AccessDeniedView();

        var location = await _locationService.GetByIdAsync(model.Id);
        if (location is null) return NotFound();

        if (!ModelState.IsValid)
            return View("~/Plugins/Widgets.MarketLocator/Views/Admin/CreateOrEdit.cshtml", model);

        MapToEntity(model, location);
        await _locationService.UpdateAsync(location);

        _notificationService.SuccessNotification("Market location updated.");
        return RedirectToAction(nameof(List));
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var location = await _locationService.GetByIdAsync(id);
        if (location is null) return NotFound();

        await _locationService.DeleteAsync(location);
        _notificationService.SuccessNotification("Market location deleted.");
        return RedirectToAction(nameof(List));
    }

    // ── Mapping helpers ───────────────────────────────────────────────────────

    private static MarketLocationModel MapToModel(MarketLocation e) => new()
    {
        Id = e.Id,
        Name = e.Name,
        Address = e.Address,
        City = e.City,
        Latitude = e.Latitude,
        Longitude = e.Longitude,
        Hours = e.Hours,
        UpcomingDatesRaw = e.UpcomingDates.Replace("|", "\n"),
        Frequency = e.Frequency,
        Published = e.Published,
        DisplayOrder = e.DisplayOrder,
    };

    private static MarketLocation MapToEntity(MarketLocationModel m, MarketLocation e)
    {
        e.Name = m.Name;
        e.Address = m.Address;
        e.City = m.City;
        e.Latitude = m.Latitude;
        e.Longitude = m.Longitude;
        e.Hours = m.Hours;
        // Convert textarea newlines → pipe-delimited storage
        e.UpcomingDates = string.Join("|",
            m.UpcomingDatesRaw.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                              .Select(d => d.Trim()));
        e.Frequency = m.Frequency;
        e.Published = m.Published;
        e.DisplayOrder = m.DisplayOrder;
        return e;
    }
}
