using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Nop.Core.Infrastructure;
using Nop.Services.Catalog;
using Nop.Services.Media;
using Nop.Plugin.Widgets.ShoppableBanner.Models;
using Nop.Web.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Widgets.ShoppableBanner.Controllers
{
    public class WidgetsShoppableBannerPublicController : BasePublicController
    {
        private readonly IProductService _productService;
        private readonly IPictureService _pictureService;
        private readonly IPriceFormatter _priceFormatter;

        public WidgetsShoppableBannerPublicController(
            IProductService productService,
            IPictureService pictureService,
            IPriceFormatter priceFormatter)
        {
            _productService = productService;
            _pictureService = pictureService;
            _priceFormatter = priceFormatter;
        }

        [HttpGet]
        public async Task<IActionResult> QuickView(int productId)
        {
            var product = await _productService.GetProductByIdAsync(productId);

            if (product == null || product.Deleted || !product.Published)
                return NotFound();

            // Get product picture
            var productPictures = await _productService.GetProductPicturesByProductIdAsync(product.Id);
            var firstProductPicture = productPictures.FirstOrDefault();
            var pictureUrl = string.Empty;
            if (firstProductPicture != null)
            {
                var picture = await _pictureService.GetPictureByIdAsync(firstProductPicture.PictureId);
                if (picture != null)
                {
                    pictureUrl = (await _pictureService.GetPictureUrlAsync(picture, 200)).Url;
                }
            }

            // Format price
            var formattedPrice = await _priceFormatter.FormatPriceAsync(product.Price);

            var model = new QuickViewProductModel
            {
                Id = product.Id,
                Name = product.Name,
                ShortDescription = product.ShortDescription,
                FullDescription = product.FullDescription,
                Sku = product.Sku,
                Price = formattedPrice,
                PictureUrl = pictureUrl
            };

            return PartialView("~/Plugins/Widgets.ShoppableBanner/Views/_QuickView.cshtml", model);
        }
    }
}
