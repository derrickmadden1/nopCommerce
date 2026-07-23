using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.ExternalAuth.Apple.Infrastructure;

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
        endpointRouteBuilder.MapControllerRoute(AppleAuthenticationDefaults.DataDeletionCallbackRoute, $"Apple/data-deletion-callback/",
            new { controller = "AppleDataDeletion", action = "DataDeletionCallback" });

        endpointRouteBuilder.MapControllerRoute(AppleAuthenticationDefaults.DataDeletionStatusCheckRoute, $"Apple/data-deletion-status-check/{{earId:min(0)}}",
            new { controller = "AppleAuthentication", action = "DataDeletionStatusCheck" });
    }

    /// <summary>
    /// Gets a priority of route provider
    /// </summary>
    public int Priority => 0;
}