using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.Widgets.ImagePuzzle.Infrastructure;
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
        endpointRouteBuilder.MapControllerRoute(name: "Plugin.Widgets.ImagePuzzle.ApplyPuzzleDiscount",
            pattern: "Plugins/ImagePuzzle/ApplyPuzzleDiscount",
            defaults: new { controller = "Puzzle", action = "ApplyPuzzleDiscount" });

        endpointRouteBuilder.MapControllerRoute(name: "Plugin.Widgets.ImagePuzzle.MarkAsSolved",
            pattern: "Plugins/ImagePuzzle/MarkAsSolved",
            defaults: new { controller = "Puzzle", action = "MarkAsSolved" });
    }

    /// <summary>
    /// Gets a priority of route provider
    /// </summary>
    public int Priority => 0;
}