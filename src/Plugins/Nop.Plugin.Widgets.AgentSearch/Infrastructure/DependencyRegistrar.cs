using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Infrastructure;
using Nop.Plugin.Widgets.AgentSearch.Services;

namespace Nop.Plugin.Widgets.AgentSearch.Infrastructure
{
    public class DependencyRegistrar : INopStartup
    {
        public int Order => 3000; // after core nopCommerce services

        public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            services.AddSingleton(provider =>
            {
                var appSettings = provider.GetService<Nop.Core.Configuration.AppSettings>();
                var config = appSettings?.Get<AzureSearchConfig>();
                if (config != null && !string.IsNullOrWhiteSpace(config.ServiceEndpoint))
                {
                    return new AzureSearchServiceOptions
                    {
                        ServiceEndpoint = config.ServiceEndpoint,
                        IndexName = config.IndexName,
                        ApiKey = config.ApiKey
                    };
                }

                return configuration.GetSection("AzureSearch").Get<AzureSearchServiceOptions>()
                       ?? new AzureSearchServiceOptions();
            });

            services.AddScoped<IAzureProductSearchClient, AzureProductSearchClient>();
            services.AddScoped<IQueryUnderstandingService, PassthroughQueryUnderstandingService>();
            services.AddScoped<IAgentSearchService, AgentSearchService>();
        }

        public void Configure(IApplicationBuilder application)
        {
            // no middleware needed — attribute routing on the controller handles this
        }
    }
}
