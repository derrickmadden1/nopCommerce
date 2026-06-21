using Microsoft.AspNetCore.Mvc;
using Nop.Plugin.Widgets.SeoEnhancements.Domain;
using Nop.Plugin.Widgets.SeoEnhancements.Services;
using Nop.Web.Framework.Components;
using Nop.Web.Models.Catalog;

namespace Nop.Plugin.Widgets.SeoEnhancements.Components;

[ViewComponent(Name = "SeoFaq")]
public class SeoFaqViewComponent : NopViewComponent
{
    private readonly IFaqService _faqService;

    public SeoFaqViewComponent(IFaqService faqService)
    {
        _faqService = faqService;
    }

    public async Task<IViewComponentResult> InvokeAsync(string widgetZone, object? additionalData = null)
    {
        IList<SeoFaqItem> faqs = new List<SeoFaqItem>();

        if (additionalData is ProductDetailsModel productModel)
            faqs = await _faqService.GetFaqItemsAsync(SeoFaqEntityType.Product, productModel.Id);
        else if (additionalData is CategoryModel categoryModel)
            faqs = await _faqService.GetFaqItemsAsync(SeoFaqEntityType.Category, categoryModel.Id);

        if (!faqs.Any())
            return Content(string.Empty);

        return View("~/Plugins/Widgets.SeoEnhancements/Views/Components/SeoFaq/Default.cshtml", faqs);
    }
}
