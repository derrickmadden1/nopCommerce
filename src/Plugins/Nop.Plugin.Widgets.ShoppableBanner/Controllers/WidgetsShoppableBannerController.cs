using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Infrastructure;
using Nop.Plugin.Widgets.ShoppableBanner.Models;
using Nop.Services.Catalog;
using Nop.Services.Configuration;
using Nop.Services.Media;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Models.DataTables;
using Nop.Web.Framework.Mvc;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Widgets.ShoppableBanner.Controllers
{
    [AuthorizeAdmin]
    [Area(AreaNames.ADMIN)]
    [AutoValidateAntiforgeryToken]
    public class WidgetsShoppableBannerController : BasePluginController
    {
        private readonly ISettingService _settingService;
        private readonly IPictureService _pictureService;
        private readonly IStoreContext _storeContext;

        public WidgetsShoppableBannerController(
            ISettingService settingService,
            IPictureService pictureService,
            IStoreContext storeContext)
        {
            _settingService = settingService;
            _pictureService = pictureService;
            _storeContext = storeContext;
        }

        public async Task<IActionResult> Configure()
        {
            var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var settings = await _settingService.LoadSettingAsync<ShoppableBannerSettings>(storeScope);

            var model = new ConfigurationModel
            {
                HeroTitle = settings.HeroTitle,
                SubText = settings.SubText,
                BackgroundPictureId = settings.BackgroundPictureId,
                ActiveStoreScopeConfiguration = storeScope
            };

            model.HotspotSearchModel.SetGridPageSize();

            if (model.BackgroundPictureId > 0)
            {
                var picture = await _pictureService.GetPictureByIdAsync(model.BackgroundPictureId);
                if (picture != null)
                {
                    model.BackgroundPictureUrl = (await _pictureService.GetPictureUrlAsync(picture)).Url;
                }

                var productService = EngineContext.Current.Resolve<IProductService>();
                foreach (var hotspot in settings.Hotspots)
                {
                    var product = await productService.GetProductByIdAsync(hotspot.ProductId);
                    model.Hotspots.Add(new HotspotListModel
                    {
                        Id = hotspot.ProductId,
                        ProductId = hotspot.ProductId,
                        ProductName = product?.Name ?? "Unknown Product",
                        PositionX = hotspot.PositionX,
                        PositionY = hotspot.PositionY
                    });
                }
            }

            return View("~/Plugins/Widgets.ShoppableBanner/Views/Admin/Configure.cshtml", model);
        }

        [HttpPost]
        public async Task<IActionResult> Configure(ConfigurationModel model)
        {
            var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var settings = await _settingService.LoadSettingAsync<ShoppableBannerSettings>(storeScope);

            settings.HeroTitle = model.HeroTitle;
            settings.SubText = model.SubText;
            settings.BackgroundPictureId = model.BackgroundPictureId;

            await _settingService.SaveSettingAsync(settings, storeScope);
            await _settingService.ClearCacheAsync();

            return RedirectToAction("Configure");
        }

        // Endpoint to populate the DataTables grid
        [HttpPost]
        public async Task<IActionResult> HotspotList(HotspotSearchModel searchModel)
        {
            var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var settings = await _settingService.LoadSettingAsync<ShoppableBannerSettings>(storeScope);
            var productService = EngineContext.Current.Resolve<IProductService>();

            var hotspotsList = new List<HotspotListModel>();

            foreach (var hotspot in settings.Hotspots)
            {
                var product = await productService.GetProductByIdAsync(hotspot.ProductId);
                hotspotsList.Add(new HotspotListModel
                {
                    Id = hotspot.ProductId, // Using ProductId as the unique row identifier for the grid
                    ProductId = hotspot.ProductId,
                    ProductName = product?.Name ?? "Unknown Product",
                    PositionX = hotspot.PositionX,
                    PositionY = hotspot.PositionY
                });
            }

            var model = new HotspotPagedListModel
            {
                Data = hotspotsList,
                RecordsTotal = hotspotsList.Count,
                RecordsFiltered = hotspotsList.Count,
                Draw = searchModel.Draw
            };

            return Json(model);
        }

        // Endpoint to add a new hotspot from the UI
        [HttpPost]
        public async Task<IActionResult> HotspotAdd(int productId, decimal positionX, decimal positionY)
        {
            if (productId <= 0)
                return Json(new { success = false, message = "Valid Product ID is required." });

            var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var settings = await _settingService.LoadSettingAsync<ShoppableBannerSettings>(storeScope);

            // Prevent duplicates for the same product
            if (settings.Hotspots.Any(x => x.ProductId == productId))
                return Json(new { success = false, message = "Product is already mapped on the banner." });

            settings.Hotspots.Add(new HotspotRecord
            {
                ProductId = productId,
                PositionX = positionX,
                PositionY = positionY
            });

            await _settingService.SaveSettingAsync(settings, storeScope);
            await _settingService.ClearCacheAsync();

            return Json(new { success = true });
        }

        // Endpoint to delete a hotspot from the grid
        [HttpPost]
        public async Task<IActionResult> HotspotDelete(int id)
        {
            var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var settings = await _settingService.LoadSettingAsync<ShoppableBannerSettings>(storeScope);

            var hotspot = settings.Hotspots.FirstOrDefault(x => x.ProductId == id);
            if (hotspot != null)
            {
                settings.Hotspots.Remove(hotspot);
                await _settingService.SaveSettingAsync(settings, storeScope);
                await _settingService.ClearCacheAsync();
            }

            return new NullJsonResult();
        }

        // Endpoint to update a hotspot's coordinates
        [HttpPost]
        public async Task<IActionResult> HotspotUpdate(int productId, decimal positionX, decimal positionY)
        {
            var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var settings = await _settingService.LoadSettingAsync<ShoppableBannerSettings>(storeScope);

            var hotspot = settings.Hotspots.FirstOrDefault(x => x.ProductId == productId);
            if (hotspot == null)
                return Json(new { success = false, message = "Hotspot not found." });

            hotspot.PositionX = positionX;
            hotspot.PositionY = positionY;

            await _settingService.SaveSettingAsync(settings, storeScope);
            await _settingService.ClearCacheAsync();

            return Json(new { success = true });
        }

    }
}