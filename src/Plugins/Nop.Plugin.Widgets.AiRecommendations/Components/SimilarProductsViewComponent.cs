using Microsoft.AspNetCore.Mvc;
using Nop.Plugin.Widgets.AiRecommendations.Models;
using Nop.Plugin.Widgets.AiRecommendations.Services;
using Nop.Services.Catalog;
using Nop.Services.Media;
using Nop.Services.Seo;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.Widgets.AiRecommendations.Components;

/// <summary>
/// Renders on product pages — "You might also like"
/// Widget zone: productdetails_bottom
/// </summary>
public class SimilarProductsViewComponent : NopViewComponent
{
    private readonly RecommendationService _recommendationService;
    private readonly AiRecommendationsSettings _settings;
    private readonly IUrlRecordService _urlRecordService;
    private readonly IPictureService _pictureService;
    private readonly IPriceFormatter _priceFormatter;

    public SimilarProductsViewComponent(
        RecommendationService recommendationService,
        AiRecommendationsSettings settings,
        IUrlRecordService urlRecordService,
        IPictureService pictureService,
        IPriceFormatter priceFormatter)
    {
        _recommendationService = recommendationService;
        _settings = settings;
        _urlRecordService = urlRecordService;
        _pictureService = pictureService;
        _priceFormatter = priceFormatter;
    }

    public async Task<IViewComponentResult> InvokeAsync(string widgetZone, object? additionalData = null)
    {
        if (!_settings.Enabled || !_settings.ShowOnProductPage)
            return Content(string.Empty);

        // additionalData contains the current product ID when invoked from a product page
        if (additionalData is not int productId)
            return Content(string.Empty);

        var products = await _recommendationService.GetSimilarProductsAsync(productId);
        if (!products.Any())
            return Content(string.Empty);

        var model = new RecommendationModel { Title = "You might also like" };

        foreach (var product in products)
        {
            var seName = await _urlRecordService.GetSeNameAsync(product);
            var picture = (await _pictureService.GetPicturesByProductIdAsync(product.Id, 1)).FirstOrDefault();
            var pictureUrl = picture != null
                ? await _pictureService.GetPictureUrlAsync(picture.Id, 200)
                : await _pictureService.GetDefaultPictureUrlAsync(200);

            model.Products.Add(new RecommendedProduct
            {
                Id = product.Id,
                Name = product.Name,
                SeName = seName,
                PictureUrl = pictureUrl,
                Price = await _priceFormatter.FormatPriceAsync(product.Price)
            });
        }

        return View("~/Plugins/Widgets.AiRecommendations/Views/SimilarProducts.cshtml", model);
    }
}
