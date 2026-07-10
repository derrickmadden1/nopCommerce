using Microsoft.AspNetCore.Mvc;
using Nop.Plugin.Widgets.AiRecommendations.Models;
using Nop.Plugin.Widgets.AiRecommendations.Services;
using Nop.Services.Catalog;
using Nop.Services.Customers;
using Nop.Services.Media;
using Nop.Services.Seo;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.Widgets.AiRecommendations.Components;

/// <summary>
/// Renders on homepage and cart — personalised or popular fallback
/// Widget zones: home_page_before_news, order_summary_content_before
/// </summary>
public class PersonalisedRecommendationsViewComponent : NopViewComponent
{
    private readonly RecommendationService _recommendationService;
    private readonly AiRecommendationsSettings _settings;
    private readonly ICustomerService _customerService;
    private readonly IUrlRecordService _urlRecordService;
    private readonly IPictureService _pictureService;
    private readonly IPriceFormatter _priceFormatter;
    private readonly Nop.Core.IWorkContext _workContext;

    public PersonalisedRecommendationsViewComponent(
        RecommendationService recommendationService,
        AiRecommendationsSettings settings,
        ICustomerService customerService,
        IUrlRecordService urlRecordService,
        IPictureService pictureService,
        IPriceFormatter priceFormatter,
        Nop.Core.IWorkContext workContext)
    {
        _recommendationService = recommendationService;
        _settings = settings;
        _customerService = customerService;
        _urlRecordService = urlRecordService;
        _pictureService = pictureService;
        _priceFormatter = priceFormatter;
        _workContext = workContext;
    }

    public async Task<IViewComponentResult> InvokeAsync(string widgetZone, object? additionalData = null)
    {
        if (!_settings.Enabled) return Content(string.Empty);

        var isHomepage = widgetZone.Contains("home_page");
        var isCart = widgetZone.Contains("order_summary");

        if (isHomepage && !_settings.ShowOnHomepage) return Content(string.Empty);
        if (isCart && !_settings.ShowOnCart) return Content(string.Empty);

        var customer = await _workContext.GetCurrentCustomerAsync();
        var isGuest = await _customerService.IsGuestAsync(customer);

        // Cart page — exclude products already in cart
        List<int>? excludeIds = null;
        if (isCart && additionalData is Nop.Web.Models.ShoppingCart.ShoppingCartModel cartModel)
        {
            excludeIds = cartModel.Items.Select(x => x.ProductId).Distinct().ToList();
        }

        IList<Nop.Core.Domain.Catalog.Product> products;

        if (isGuest)
        {
            // Anonymous visitor — show popular/newest products
            products = await _recommendationService.GetPersonalisedRecommendationsAsync(0);
        }
        else
        {
            products = await _recommendationService.GetPersonalisedRecommendationsAsync(
                customer.Id, excludeProductIds: excludeIds);
        }

        if (!products.Any()) return Content(string.Empty);

        var title = isGuest
            ? "Popular right now"
            : isCart
                ? "Complete your order"
                : "Recommended for you";

        var model = new RecommendationModel { Title = title };

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

        return View("~/Plugins/Widgets.AiRecommendations/Views/Recommendations.cshtml", model);
    }
}
