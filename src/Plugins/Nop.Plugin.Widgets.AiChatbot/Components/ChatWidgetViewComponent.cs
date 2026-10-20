using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Plugin.Widgets.AiChatbot.Services;
using Nop.Services.Customers;
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
    private readonly ICustomerService _customerService;
    private readonly ChatSessionService _chatSessionService;

    public ChatWidgetViewComponent(
        AiChatbotSettings settings,
        IShoppingCartService shoppingCartService,
        IStoreContext storeContext,
        IWorkContext workContext,
        ICustomerService customerService,
        ChatSessionService chatSessionService)
    {
        _settings = settings;
        _shoppingCartService = shoppingCartService;
        _storeContext = storeContext;
        _workContext = workContext;
        _customerService = customerService;
        _chatSessionService = chatSessionService;
    }

    public async Task<IViewComponentResult> InvokeAsync(string widgetZone, object? additionalData = null)
    {
        if (!_settings.Enabled)
            return Content(string.Empty);

        var controller = ViewContext.RouteData.Values["controller"]?.ToString();
        var isCheckoutPage = string.Equals(controller, "Checkout", StringComparison.OrdinalIgnoreCase);
        ViewBag.IsCheckoutPage = isCheckoutPage;

        var customer = await _workContext.GetCurrentCustomerAsync();
        var store = await _storeContext.GetCurrentStoreAsync();
        var cart = await _shoppingCartService.GetShoppingCartAsync(customer, ShoppingCartType.ShoppingCart, store.Id);

        ViewBag.HasCartItems = cart.Any();

        var isGuest = customer == null || await _customerService.IsGuestAsync(customer);
        var customerId = isGuest ? 0 : customer!.Id;
        ViewBag.CustomerId = customerId;

        if (customerId > 0)
        {
            var recentSession = await _chatSessionService.GetRecentCustomerSessionAsync(customerId, 24);
            if (recentSession != null)
            {
                ViewBag.ActiveSessionGuid = recentSession.SessionGuid.ToString();
                ViewBag.ActiveSessionHistory = recentSession.MessagesJson;
            }
        }

        return View("~/Plugins/Widgets.AiChatbot/Views/ChatWidget.cshtml", _settings);
    }
}
