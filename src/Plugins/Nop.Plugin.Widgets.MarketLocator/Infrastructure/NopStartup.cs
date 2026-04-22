using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Infrastructure;
using Nop.Plugin.Widgets.MarketLocator.Services;

namespace Nop.Plugin.Widgets.MarketLocator.Infrastructure;

public class NopStartup : INopStartup
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IMarketLocationService, MarketLocationService>();
        services.AddScoped<IIcsBuilder, IcsBuilder>();
        services.AddScoped<IMarketPickupService, MarketPickupService>();
    }

    public void Configure(IApplicationBuilder application) { }

    public int Order => 3000;
}
