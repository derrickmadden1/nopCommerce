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
                name: "Plugin.Misc.UniversalCommerce.Catalog",
                pattern: "api/ucp/catalog",
                defaults: new { controller = "UcpApi", action = "GetCatalog" });

            endpointRouteBuilder.MapControllerRoute(
                name: "Plugin.Misc.UniversalCommerce.Checkout",
                pattern: "api/ucp/checkout",
                defaults: new { controller = "UcpApi", action = "AgentCheckout" });
        }

        public int Priority => 3000; // Overrides basic route evaluation bindings
    }
}
