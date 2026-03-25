using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Nop.Core.Domain.Orders;
using Nop.Core.Infrastructure;
using Nop.Plugin.Widgets.ImagePuzzle.Components;
using Nop.Services.Cms;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Discounts;
using Nop.Services.Localization;
using Nop.Services.Orders;
using Nop.Services.Plugins;
using Nop.Web.Framework.Infrastructure;

namespace Nop.Plugin.Widgets.ImagePuzzle;

public partial class ImagePuzzlePlugin : BasePlugin, IWidgetPlugin, IDiscountRequirementRule
{
    private readonly IGenericAttributeService _genericAttributeService;
    private readonly ILocalizationService _localizationService;
    private readonly ISettingService _settingService;
    private readonly IShoppingCartService _shoppingCartService;

    public ImagePuzzlePlugin(IGenericAttributeService genericAttributeService,
        ILocalizationService localizationService,
        ISettingService settingService,
        IShoppingCartService shoppingCartService)
    {
        _genericAttributeService = genericAttributeService;
        _localizationService = localizationService;
        _settingService = settingService;
        _shoppingCartService = shoppingCartService;
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

        var customer = request.Customer;
        var solvedIdsString = await _genericAttributeService.GetAttributeAsync<string>(customer, "SolvedPuzzleProductIds") ?? "";
        var solvedIds = solvedIdsString.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList();

        // Check if the customer has solved any puzzle at all.
        // The more granular filtering happens in PuzzlePriceCalculationService where we know the current product.
        bool hasSolvedAnyPuzzle = solvedIds.Any();

        return new DiscountRequirementValidationResult
        {
            IsValid = hasSolvedAnyPuzzle,
            UserError = await _localizationService.GetResourceAsync("Plugins.Widgets.ImagePuzzle.RequirementError") ?? "This discount only applies to products you have solved the puzzle for!"
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