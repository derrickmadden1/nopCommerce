using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
        var appSettings = services.BuildServiceProvider().GetRequiredService<AppSettings>();
        var config = appSettings.Get<MarketLocatorConfig>();

        if (config != null && !string.IsNullOrEmpty(config.ServiceBusConnectionString))
        {
            services.AddSingleton<IServiceBusPublisher>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<ServiceBusPublisher>>();
                var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                var fbQueue = string.IsNullOrEmpty(config.QueueName) ? "market-social-posts" : config.QueueName;
                map[fbQueue] = config.ServiceBusConnectionString;

                var igQueue = string.IsNullOrEmpty(config.InstagramQueueName) ? "market-instagram-posts" : config.InstagramQueueName;
                if (!string.IsNullOrEmpty(config.InstagramServiceBusConnectionString))
                {
                    map[igQueue] = config.InstagramServiceBusConnectionString;
                }
                else
                {
                    // Auto-derive Instagram connection string by swapping EntityPath=... with EntityPath=igQueue
                    map[igQueue] = Regex.Replace(
                        config.ServiceBusConnectionString,
                        @"EntityPath=[^;]+",
                        $"EntityPath={igQueue}",
                        RegexOptions.IgnoreCase);
                }

                return new ServiceBusPublisher(logger, map);
            });
        }

        services.AddScoped<IMarketLocationService, MarketLocationService>();
        services.AddScoped<IIcsBuilder, IcsBuilder>();
        services.AddScoped<IMarketPickupService, MarketPickupService>();
    }

    public void Configure(IApplicationBuilder application) { }

    public int Order => 3000;
}
