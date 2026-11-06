using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Services.Customers;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.Widgets.AiChatbot.Components;

/// <summary>
/// Injects the chat bubble into every page via body_end_html_tag_before widget zone
/// </summary>
public class ChatWidgetViewComponent : NopViewComponent
{
    private readonly AiChatbotSettings _settings;
    private readonly IWorkContext _workContext;
    private readonly ICustomerService _customerService;

    public ChatWidgetViewComponent(
        AiChatbotSettings settings,
        IWorkContext workContext,
        ICustomerService customerService)
    {
        _settings = settings;
        _workContext = workContext;
        _customerService = customerService;
    }

    public async Task<IViewComponentResult> InvokeAsync(string widgetZone, object? additionalData = null)
    {
        if (!_settings.Enabled)
            return Content(string.Empty);

        var controller = ViewContext.RouteData.Values["controller"]?.ToString();
        var isCheckoutPage = string.Equals(controller, "Checkout", StringComparison.OrdinalIgnoreCase);
        ViewBag.IsCheckoutPage = isCheckoutPage;

        var customer = await _workContext.GetCurrentCustomerAsync();
        var isGuest = customer == null || await _customerService.IsGuestAsync(customer);
        var customerId = isGuest ? 0 : customer!.Id;
        ViewBag.CustomerId = customerId;

        return View("~/Plugins/Widgets.AiChatbot/Views/ChatWidget.cshtml", _settings);
    }
}
