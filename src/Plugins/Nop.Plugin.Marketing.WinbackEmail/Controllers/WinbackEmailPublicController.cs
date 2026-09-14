using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Services.Common;
using Nop.Services.Customers;
using Nop.Services.Messages;
using Nop.Web.Controllers;
using Nop.Web.Framework.Controllers;

namespace Nop.Plugin.Marketing.WinbackEmail.Controllers;

public class WinbackEmailPublicController : BasePublicController
{
    private readonly ICustomerService _customerService;
    private readonly INewsLetterSubscriptionService _newsletterSubscriptionService;
    private readonly IGenericAttributeService _genericAttributeService;
    private readonly IStoreContext _storeContext;

    public WinbackEmailPublicController(
        ICustomerService customerService,
        INewsLetterSubscriptionService newsletterSubscriptionService,
        IGenericAttributeService genericAttributeService,
        IStoreContext storeContext)
    {
        _customerService = customerService;
        _newsletterSubscriptionService = newsletterSubscriptionService;
        _genericAttributeService = genericAttributeService;
        _storeContext = storeContext;
    }

    [HttpGet]
    public async Task<IActionResult> Unsubscribe(string email, string token)
    {
        if (string.IsNullOrWhiteSpace(email))
            return RedirectToRoute("Homepage");

        var store = await _storeContext.GetCurrentStoreAsync();

        // Deactivate newsletter subscriptions for this email
        var subscriptions = await _newsletterSubscriptionService.GetNewsLetterSubscriptionsByEmailAsync(email, store.Id);
        foreach (var subscription in subscriptions)
        {
            if (subscription.Active)
            {
                subscription.Active = false;
                await _newsletterSubscriptionService.UpdateNewsLetterSubscriptionAsync(subscription);
            }
        }

        // Flag customer entity as unsubscribed from winback emails
        var customer = await _customerService.GetCustomerByEmailAsync(email);
        if (customer != null)
        {
            await _genericAttributeService.SaveAttributeAsync(customer, "Winback_Unsubscribed", true);
        }

        ViewBag.Email = email;
        return View("~/Plugins/Marketing.WinbackEmail/Views/Unsubscribe.cshtml");
    }
}
