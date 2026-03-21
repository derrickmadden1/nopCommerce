using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Infrastructure;
using Nop.Plugin.Widgets.ImagePuzzle;
using Nop.Services.Configuration;
using Nop.Core.Domain.Cms;
using Nop.Core;

namespace Nop.Plugin.Widgets.ImagePuzzle.Infrastructure;

public class PluginNopStartup : INopStartup
{
    /// <summary>
    /// Add and configure any of the middleware
    /// </summary>
    /// <param name="services">Collection of service descriptors</param>
    /// <param name="configuration">Configuration of the application</param>
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RazorViewEngineOptions>(options =>
        {
            options.ViewLocationExpanders.Add(new ViewLocationExpander());
        });

        //register services and interfaces
    }

    /// <summary>
    /// Configure the using of added middleware
    /// </summary>
    /// <param name="application">Builder for configuring an application's request pipeline</param>
    public void Configure(IApplicationBuilder application)
    {
        // Re-force activation if not already active (helps with debugging already installed plugin)
        try 
        {
            var settingService = EngineContext.Current.Resolve<ISettingService>();
            if (settingService != null)
            {
                var widgetSettings = settingService.LoadSettingAsync<WidgetSettings>().GetAwaiter().GetResult();
                if (!widgetSettings.ActiveWidgetSystemNames.Contains("Widgets.ImagePuzzle"))
                {
                    widgetSettings.ActiveWidgetSystemNames.Add("Widgets.ImagePuzzle");
                    settingService.SaveSettingAsync(widgetSettings).GetAwaiter().GetResult();
                }

                // Ensure PuzzleSettings also exist
                var puzzleSettings = settingService.LoadSettingAsync<PuzzleSettings>().GetAwaiter().GetResult();
                if (puzzleSettings.GridSize == 0)
                {
                    puzzleSettings.GridSize = 3;
                    settingService.SaveSettingAsync(puzzleSettings).GetAwaiter().GetResult();
                }
            }
        }
        catch { /* Ignore errors during startup self-activation to prevent site crash */ }
    }

    /// <summary>
    /// Gets order of this startup configuration implementation
    /// </summary>
    public int Order => 3000;
}