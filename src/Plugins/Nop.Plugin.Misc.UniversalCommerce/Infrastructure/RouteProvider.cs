using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.Misc.UniversalCommerce.Infrastructure
{
    public class RouteProvider : IRouteProvider
    {
        public void RegisterRoutes(IEndpointRouteBuilder endpointRouteBuilder)
        {
            // Standard root path handling for the Google Agent crawler
            endpointRouteBuilder.MapControllerRoute(
                name: "Plugin.Misc.UniversalCommerce.WellKnown",
                pattern: ".well-known/ucp",
                defaults: new { controller = "UcpApi", action = "GetManifest" });

            endpointRouteBuilder.MapControllerRoute(
                name: "Plugin.Misc.UniversalCommerce.CatalogSearchPost",
                pattern: "api/ucp/v1/catalog/search",
                defaults: new { controller = "UcpApi", action = "CatalogSearch" });

            endpointRouteBuilder.MapControllerRoute(
                name: "Plugin.Misc.UniversalCommerce.ProductsGet",
                pattern: "api/ucp/v1/products",
                defaults: new { controller = "UcpApi", action = "ProductsGet" });

            endpointRouteBuilder.MapControllerRoute(
                name: "Plugin.Misc.UniversalCommerce.Checkout",
                pattern: "api/ucp/v1/checkout",
                defaults: new { controller = "UcpApi", action = "AgentCheckout" });
        }

        public int Priority => 3000; // Overrides basic route evaluation bindings
    }
}
