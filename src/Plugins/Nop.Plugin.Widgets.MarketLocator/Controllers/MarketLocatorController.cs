using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Plugin.Widgets.MarketLocator.Services;
using Nop.Services.Configuration;

namespace Nop.Plugin.Widgets.MarketLocator.Controllers;

public class MarketLocatorController : Controller
{
    private readonly IMarketLocationService _locationService;
    private readonly ISettingService _settingService;
    private readonly IWebHelper _webHelper;
    private readonly IIcsBuilder _icsBuilder;

    public MarketLocatorController(
        IMarketLocationService locationService,
        ISettingService settingService,
        IWebHelper webHelper,
        IIcsBuilder icsBuilder)
    {
        _locationService = locationService;
        _settingService = settingService;
        _webHelper = webHelper;
        _icsBuilder = icsBuilder;
    }

    /// <summary>GET /market-locations — renders the full-page map.</summary>
    [HttpGet, Route("market-locations")]
    public async Task<IActionResult> Index()
    {
        var settings = await _settingService.LoadSettingAsync<MarketLocatorSettings>();
        ViewBag.AzureMapsKey = settings.AzureMapsKey;
        ViewBag.DefaultZoom  = settings.DefaultZoom;
        ViewBag.DefaultLat   = settings.DefaultLatitude;
        ViewBag.DefaultLng   = settings.DefaultLongitude;
        return View("~/Plugins/Widgets.MarketLocator/Views/MarketLocator/Index.cshtml");
    }

    /// <summary>GET /market-locations/data — JSON feed for the Leaflet front-end.</summary>
    [HttpGet, Route("market-locations/data")]
    [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> Data()
    {
        var dtos = await _locationService.GetPublishedDtosAsync();
        return Json(dtos);
    }

    /// <summary>
    /// GET /market-locations/ics/{id}
    /// Downloads a .ics file for a single market (all upcoming dates).
    /// </summary>
    [HttpGet, Route("market-locations/ics/{id:int}")]
    public async Task<IActionResult> IcsSingle(int id)
    {
        var location = await _locationService.GetByIdAsync(id);
        if (location is null || !location.Published)
            return NotFound();

        var ics      = _icsBuilder.BuildForLocation(location, _webHelper.GetStoreLocation());
        var fileName = $"{SanitiseFileName(location.Name)}-market-dates.ics";
        return File(System.Text.Encoding.UTF8.GetBytes(ics), "text/calendar; charset=utf-8", fileName);
    }

    /// <summary>
    /// GET /market-locations/ics
    /// Subscribable .ics feed of ALL published locations.
    /// Paste this URL into Google Calendar / Apple Calendar / Outlook "Subscribe by URL".
    /// </summary>
    [HttpGet, Route("market-locations/ics")]
    public async Task<IActionResult> IcsAll()
    {
        var locations = await _locationService.GetAllAsync(showUnpublished: false);
        var ics       = _icsBuilder.BuildForAll(locations, _webHelper.GetStoreLocation());
        Response.Headers["Content-Disposition"] = "inline; filename=\"market-locations.ics\"";
        return Content(ics, "text/calendar; charset=utf-8");
    }

    private static string SanitiseFileName(string name) =>
        System.Text.RegularExpressions.Regex.Replace(name, @"[^\w\-]", "-")
              .Trim('-').ToLowerInvariant();
}
