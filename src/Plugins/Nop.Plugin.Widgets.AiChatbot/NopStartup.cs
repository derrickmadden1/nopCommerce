using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Infrastructure;
using Nop.Plugin.Widgets.AiChatbot.Services;

namespace Nop.Plugin.Widgets.AiChatbot.Infrastructure;

public class NopStartup : INopStartup
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<CustomerContextService>();
        services.AddScoped<ProductSearchService>();
        services.AddScoped<ChatService>();
    }

    public void Configure(IApplicationBuilder app) { }

    public int Order => 100;
}
