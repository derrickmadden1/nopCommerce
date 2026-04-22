using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Infrastructure;

namespace Nop.Plugin.Search.AzureAI.Infrastructure;

public class NopStartup : INopStartup
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<AzureAISearchService>();
        services.AddScoped<ServiceBusPublisher>();
    }

    public void Configure(IApplicationBuilder app)
    {
        // Nothing needed here
    }

    public int Order => 100;
}
