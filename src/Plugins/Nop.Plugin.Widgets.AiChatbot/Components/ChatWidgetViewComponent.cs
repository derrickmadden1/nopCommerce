using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Services.Orders;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.Widgets.AiChatbot.Components;

/// <summary>
/// Injects the chat bubble into every page via body_end_html_tag_before widget zone
/// </summary>
public class ChatWidgetViewComponent : NopViewComponent
{
    private readonly AiChatbotSettings _settings;
    private readonly IShoppingCartService _shoppingCartService;
    private readonly IStoreContext _storeContext;
    private readonly IWorkContext _workContext;

    public ChatWidgetViewComponent(
        AiChatbotSettings settings,
        IShoppingCartService shoppingCartService,
        IStoreContext storeContext,
        IWorkContext workContext)
    {
        _settings = settings;
        _shoppingCartService = shoppingCartService;
        _storeContext = storeContext;
        _workContext = workContext;
    }

    public async Task<IViewComponentResult> InvokeAsync(string widgetZone, object? additionalData = null)
    {
        if (!_settings.Enabled)
            return Content(string.Empty);

        var controller = ViewContext.RouteData.Values["controller"]?.ToString();
        if (string.Equals(controller, "Checkout", StringComparison.OrdinalIgnoreCase))
            return Content(string.Empty);

        var customer = await _workContext.GetCurrentCustomerAsync();
        var store = await _storeContext.GetCurrentStoreAsync();
        var cart = await _shoppingCartService.GetShoppingCartAsync(customer, ShoppingCartType.ShoppingCart, store.Id);

        ViewBag.HasCartItems = cart.Any();

        return View("~/Plugins/Widgets.AiChatbot/Views/ChatWidget.cshtml", _settings);
    }
}
