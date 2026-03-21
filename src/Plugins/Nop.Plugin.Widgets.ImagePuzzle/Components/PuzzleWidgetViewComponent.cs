using Microsoft.AspNetCore.Mvc;
using Nop.Web.Framework.Components;
using Nop.Plugin.Widgets.ImagePuzzle.Models;
using Nop.Services.Configuration;

namespace Nop.Plugin.Widgets.ImagePuzzle.Components;

[ViewComponent(Name = "PuzzleWidget")]
public class PuzzleWidgetViewComponent : NopViewComponent
{
    private readonly ISettingService _settingService;

    public PuzzleWidgetViewComponent(ISettingService settingService)
    {
        _settingService = settingService;
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

        var viewModel = new PuzzleModel
        {
            ProductId = model.Id,
            GridSize = settings.GridSize
        };

        return View("~/Plugins/Widgets.ImagePuzzle/Views/Default.cshtml", viewModel);
    }
}
