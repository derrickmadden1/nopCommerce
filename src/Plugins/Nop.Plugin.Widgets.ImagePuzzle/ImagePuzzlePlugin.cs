using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nop.Plugin.Widgets.ImagePuzzle.Components;
using Nop.Services.Cms;
using Nop.Services.Plugins;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Web.Framework.Infrastructure;
using Nop.Services.Common;
using Nop.Services.Discounts;
using Nop.Core.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Nop.Plugin.Widgets.ImagePuzzle;

public partial class ImagePuzzlePlugin : BasePlugin, IWidgetPlugin, IDiscountRequirementRule
{
    private readonly IGenericAttributeService _genericAttributeService;
    private readonly ILocalizationService _localizationService;
    private readonly ISettingService _settingService;

    public ImagePuzzlePlugin(IGenericAttributeService genericAttributeService,
        ILocalizationService localizationService,
        ISettingService settingService)
    {
        _genericAttributeService = genericAttributeService;
        _localizationService = localizationService;
        _settingService = settingService;
    }

    public bool HideInWidgetList => false;

    public Task<IList<string>> GetWidgetZonesAsync()
    {
        return Task.FromResult<IList<string>>(new List<string>
        {
            PublicWidgetZones.ProductDetailsBeforePictures
        });
    }

    public Type GetWidgetViewComponent(string widgetZone)
    {
        return typeof(PuzzleWidgetViewComponent);
    }

    public override async Task InstallAsync()
    {
        await _settingService.SaveSettingAsync(new PuzzleSettings
        {
            GridSize = 3
        });

        var widgetSettings = await _settingService.LoadSettingAsync<Nop.Core.Domain.Cms.WidgetSettings>();
        if (!widgetSettings.ActiveWidgetSystemNames.Contains("Widgets.ImagePuzzle"))
        {
            widgetSettings.ActiveWidgetSystemNames.Add("Widgets.ImagePuzzle");
            await _settingService.SaveSettingAsync(widgetSettings);
        }

        await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
        {
            ["Plugins.Discount.Requirements.HasSolvedPuzzle"] = "Customer has solved product puzzle",
            ["Plugins.Widgets.ImagePuzzle.RequirementError"] = "You must solve the product puzzle to use this discount!"
        });

        await base.InstallAsync();
    }

    public override async Task UninstallAsync()
    {
        await _settingService.DeleteSettingAsync<PuzzleSettings>();

        var widgetSettings = await _settingService.LoadSettingAsync<Nop.Core.Domain.Cms.WidgetSettings>();
        if (widgetSettings.ActiveWidgetSystemNames.Contains("Widgets.ImagePuzzle"))
        {
            widgetSettings.ActiveWidgetSystemNames.Remove("Widgets.ImagePuzzle");
            await _settingService.SaveSettingAsync(widgetSettings);
        }

        await _localizationService.DeleteLocaleResourceAsync("Plugins.Discount.Requirements.HasSolvedPuzzle");

        await base.UninstallAsync();
    }

    #region Discount Requirement

    public async Task<DiscountRequirementValidationResult> CheckRequirementAsync(DiscountRequirementValidationRequest request)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        // Check if the "PuzzleSolved" flag exists in the customer's generic attributes
        var isSolved = await _genericAttributeService.GetAttributeAsync<bool>(request.Customer, "PuzzleSolved");

        return new DiscountRequirementValidationResult
        {
            IsValid = isSolved,
            UserError = await _localizationService.GetResourceAsync("Plugins.Widgets.ImagePuzzle.RequirementError") ?? "You must solve the product puzzle to use this discount!"
        };
    }

    public string GetConfigurationUrl(int discountId, int? discountRequirementId)
    {
        var urlHelperFactory = EngineContext.Current.Resolve<IUrlHelperFactory>();
        var actionContextAccessor = EngineContext.Current.Resolve<IActionContextAccessor>();
        var urlHelper = urlHelperFactory.GetUrlHelper(actionContextAccessor.ActionContext);

        return urlHelper.RouteUrl("Plugin.Widgets.ImagePuzzle.ConfigureRequirement",
            new { discountId, discountRequirementId });
    }

    #endregion
}