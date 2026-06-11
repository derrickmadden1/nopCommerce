using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Nop.Web.Framework.Components;
using Nop.Services.Catalog;
using Nop.Services.Media;
using System.Threading.Tasks;
using System.Collections.Generic;
using Nop.Plugin.Widgets.ShoppableBanner.Models;

namespace Nop.Plugin.Widgets.ShoppableBanner.Components
{
    [ViewComponent(Name = "WidgetsShoppableBanner")]
    public class WidgetsShoppableBannerViewComponent : NopViewComponent
    {
        private readonly IProductService _productService;
        private readonly IPictureService _pictureService;
        private readonly ShoppableBannerSettings _settings;
        private readonly IPriceFormatter _priceFormatter;

        public WidgetsShoppableBannerViewComponent(
            IProductService productService,
            IPictureService pictureService,
            ShoppableBannerSettings settings,
            IPriceFormatter priceFormatter)
        {
            _productService = productService;
            _pictureService = pictureService;
            _settings = settings;
            _priceFormatter = priceFormatter;
        }

        public async Task<IViewComponentResult> InvokeAsync(string widgetZone, object additionalData)
        {
            var model = new ShoppableBannerModel
            {
                HeroTitle = _settings.HeroTitle,
                SubText = _settings.SubText,
                MobileSubText = _settings.MobileSubText
            };

            // Get background image URL
            var bgPicture = await _pictureService.GetPictureByIdAsync(_settings.BackgroundPictureId);
            if (bgPicture != null)
            {
                model.BackgroundPictureUrl = (await _pictureService.GetPictureUrlAsync(bgPicture)).Url;
            }

            // Fetch live product data for each hotspot
            foreach (var hotspot in _settings.Hotspots)
            {
                var product = await _productService.GetProductByIdAsync(hotspot.ProductId);
                if (product != null && !product.Deleted && product.Published)
                {
                    // Get product picture
                    var productPictures = await _productService.GetProductPicturesByProductIdAsync(product.Id);
                    var firstProductPicture = productPictures.FirstOrDefault();
                    var pictureUrl = string.Empty;
                    if (firstProductPicture != null)
                    {
                        var picture = await _pictureService.GetPictureByIdAsync(firstProductPicture.PictureId);
                        if (picture != null)
                        {
                            pictureUrl = (await _pictureService.GetPictureUrlAsync(picture, 150)).Url;
                        }
                    }

                    // Format price
                    var formattedPrice = await _priceFormatter.FormatPriceAsync(product.Price);

                    model.Hotspots.Add(new HotspotModel
                    {
                        ProductId = product.Id,
                        ProductName = product.Name,
                        PositionX = hotspot.PositionX,
                        PositionY = hotspot.PositionY,
                        PictureUrl = pictureUrl,
                        Price = formattedPrice
                    });
                }
            }

            return View("~/Plugins/Widgets.ShoppableBanner/Views/PublicInfo.cshtml", model);
        }
    }
}