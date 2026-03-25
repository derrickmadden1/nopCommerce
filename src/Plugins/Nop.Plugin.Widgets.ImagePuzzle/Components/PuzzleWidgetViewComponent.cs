using Microsoft.AspNetCore.Mvc;
using Nop.Web.Framework.Components;
using Nop.Plugin.Widgets.ImagePuzzle.Models;
using Nop.Services.Configuration;

namespace Nop.Plugin.Widgets.ImagePuzzle.Components;

[ViewComponent(Name = "PuzzleWidget")]
public class PuzzleWidgetViewComponent : NopViewComponent
{
    private readonly ISettingService _settingService;
    private readonly IPuzzleService _puzzleService;
    private readonly IProductService _productService;

    public PuzzleWidgetViewComponent(ISettingService settingService, 
        IPuzzleService puzzleService,
        IProductService productService)
    {
        _settingService = settingService;
        _puzzleService = puzzleService;
        _productService = productService;
    }

    public async Task<IViewComponentResult> InvokeAsync(string widgetZone, object additionalData)
    {
        // Debug logging
        if (additionalData == null)
            return Content("ImagePuzzle: additionalData is null");

        // We only want to trigger this if we are on a product page
        if (additionalData is not Nop.Web.Models.Catalog.ProductDetailsModel model)
            return Content($"ImagePuzzle: additionalData is not ProductDetailsModel. Type: {additionalData.GetType().Name}");

        var settings = await _settingService.LoadSettingAsync<PuzzleSettings>();
        var product = await _productService.GetProductByIdAsync(model.Id);

        var viewModel = new PuzzleModel
        {
            ProductId = model.Id,
            GridSize = settings.GridSize,
            IsMultiBuy = product != null && await _puzzleService.IsProductInMultiBuyAsync(product)
        };

        return View("~/Plugins/Widgets.ImagePuzzle/Views/Default.cshtml", viewModel);
    }
}
