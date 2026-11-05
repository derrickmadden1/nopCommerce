using Microsoft.AspNetCore.Mvc;
using Nop.Plugin.Payments.PayPalCommerce.Factories;
using Nop.Plugin.Payments.PayPalCommerce.Services;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.Payments.PayPalCommerce.Components.Public;

/// <summary>
/// Represents the view component to display PayPal Fastlane email intercept and accelerated checkout on cart
/// </summary>
public class FastlaneViewComponent : NopViewComponent
{
    #region Fields

    private readonly PayPalCommerceModelFactory _modelFactory;
    private readonly PayPalCommerceServiceManager _serviceManager;
    private readonly PayPalCommerceSettings _settings;

    #endregion

    #region Ctor

    public FastlaneViewComponent(
        PayPalCommerceModelFactory modelFactory,
        PayPalCommerceServiceManager serviceManager,
        PayPalCommerceSettings settings)
    {
        _modelFactory = modelFactory;
        _serviceManager = serviceManager;
        _settings = settings;
    }

    #endregion

    #region Methods

    public async Task<IViewComponentResult> InvokeAsync(string widgetZone, object additionalData)
    {
        var (active, _) = await _serviceManager.IsActiveAsync(_settings);
        if (!active || !_settings.UseFastlane)
            return Content(string.Empty);

        var model = await _modelFactory.PrepareFastlaneCartModelAsync();
        if (model is null)
            return Content(string.Empty);

        return View("~/Plugins/Payments.PayPalCommerce/Views/Public/_FastlaneCart.cshtml", model);
    }

    #endregion
}
