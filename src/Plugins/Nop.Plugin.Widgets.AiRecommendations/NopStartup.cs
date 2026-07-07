using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Infrastructure;
using Nop.Plugin.Widgets.AiRecommendations.Services;

namespace Nop.Plugin.Widgets.AiRecommendations.Infrastructure;

public class NopStartup : INopStartup
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<EmbeddingService>();
        services.AddScoped<RecommendationService>();
    }

    public void Configure(IApplicationBuilder app) { }

    public int Order => 100;
}
