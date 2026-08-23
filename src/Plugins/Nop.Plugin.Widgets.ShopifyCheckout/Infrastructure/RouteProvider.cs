using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Nop.Web.Framework;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.Widgets.ShopifyCheckout.Infrastructure;

/// <summary>
/// Represents plugin route provider
/// </summary>
public class RouteProvider : IRouteProvider
{
    /// <summary>
    /// Register routes
    /// </summary>
    /// <param name="endpointRouteBuilder">Route builder</param>
    public void RegisterRoutes(IEndpointRouteBuilder endpointRouteBuilder)
    {
        // Admin configuration route
        endpointRouteBuilder.MapControllerRoute(
            "Plugin.Widgets.ShopifyCheckout.Configure",
            "Admin/ShopifyCheckout/Configure",
            new { controller = "ShopifyCheckout", action = "Configure", area = AreaNames.ADMIN });

        // Auto-generate storefront token route
        endpointRouteBuilder.MapControllerRoute(
            "Plugin.Widgets.ShopifyCheckout.AutoGenerateStorefrontToken",
            "Admin/ShopifyCheckout/AutoGenerateStorefrontToken",
            new { controller = "ShopifyCheckout", action = "AutoGenerateStorefrontToken", area = AreaNames.ADMIN });

        // Full catalog sync route
        endpointRouteBuilder.MapControllerRoute(
            "Plugin.Widgets.ShopifyCheckout.RunFullCatalogSync",
            "Admin/ShopifyCheckout/RunFullCatalogSync",
            new { controller = "ShopifyCheckout", action = "RunFullCatalogSync", area = AreaNames.ADMIN });

        // Public init checkout route
        endpointRouteBuilder.MapControllerRoute(
            "Plugin.Widgets.ShopifyCheckout.InitCheckout",
            "ShopifyCheckout/InitCheckout",
            new { controller = "ShopifyCheckout", action = "InitCheckout" });

        // Webhook order sync route
        endpointRouteBuilder.MapControllerRoute(
            "Plugin.Widgets.ShopifyCheckout.ProcessOrderWebhook",
            "ShopifyCheckout/ProcessOrderWebhook",
            new { controller = "ShopifyCheckout", action = "ProcessOrderWebhook" });
    }

    /// <summary>
    /// Gets a priority of route provider
    /// </summary>
    public int Priority => 100;
}
