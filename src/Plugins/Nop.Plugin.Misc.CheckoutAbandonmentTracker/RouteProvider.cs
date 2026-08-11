using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.Misc.CheckoutAbandonmentTracker
{
    public class RouteProvider : IRouteProvider
    {
        public void RegisterRoutes(IEndpointRouteBuilder endpointRouteBuilder)
        {
            endpointRouteBuilder.MapControllerRoute("CheckoutTracking.RecordEvent",
                "checkout-tracking/record",
                new { controller = "CheckoutTracking", action = "RecordEvent" });
        }

        public int Priority => 0;
    }
}

