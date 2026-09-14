using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.Marketing.WinbackEmail.Infrastructure;

public class RouteProvider : IRouteProvider
{
    public void RegisterRoutes(IEndpointRouteBuilder endpointRouteBuilder)
    {
        endpointRouteBuilder.MapControllerRoute("Plugin.Marketing.WinbackEmail.Unsubscribe",
            "winback/unsubscribe",
            new { controller = "WinbackEmailPublic", action = "Unsubscribe" });
    }

    public int Priority => 0;
}
