using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Azure.Messaging.ServiceBus;
using Nop.Core.Configuration;
using Nop.Core.Infrastructure;
using Nop.Services.Messaging;
using Nop.Plugin.Widgets.MarketLocator.Services;

namespace Nop.Plugin.Widgets.MarketLocator.Infrastructure;

public class NopStartup : INopStartup
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // Get the config from appSettings (or other config providers)
        var appSettings = services.BuildServiceProvider().GetRequiredService<AppSettings>();
        var config = appSettings.Get<MarketLocatorConfig>();

        if (!string.IsNullOrEmpty(config.ServiceBusConnectionString))
        {
            services.AddSingleton(_ => new ServiceBusClient(config.ServiceBusConnectionString,
                new ServiceBusClientOptions
                {
                    TransportType = ServiceBusTransportType.AmqpWebSockets
                }));

            services.AddSingleton<IServiceBusPublisher, ServiceBusPublisher>();
        }

        services.AddScoped<IMarketLocationService, MarketLocationService>();
        services.AddScoped<IIcsBuilder, IcsBuilder>();
        services.AddScoped<IMarketPickupService, MarketPickupService>();
    }

    public void Configure(IApplicationBuilder application) { }

    public int Order => 3000;
}
