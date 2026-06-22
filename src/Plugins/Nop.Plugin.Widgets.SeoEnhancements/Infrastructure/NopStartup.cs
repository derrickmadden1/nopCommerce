using System.Net.Http.Headers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Infrastructure;
using Nop.Plugin.Widgets.SeoEnhancements.Services;
using Nop.Web.Framework.UI;

namespace Nop.Plugin.Widgets.SeoEnhancements.Infrastructure;

public class NopStartup : INopStartup
{
    public void ConfigureServices(IServiceCollection services, Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        services.AddScoped<IFaqService, FaqService>();
        services.AddScoped<ISchemaService, SchemaService>();
        services.AddScoped<IFaqGenerationService, FaqGenerationService>();

        // Named HttpClient for Azure OpenAI calls — generous timeout since
        // generating 5 FAQ pairs can take several seconds.
        services.AddHttpClient("SeoEnhancements.AzureOpenAi", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        });

        // Decorate INopHtmlHelper with our SEO-enhanced version
        services.AddScoped<INopHtmlHelper, SeoEnhancedNopHtmlHelper>();
    }

    public void Configure(IApplicationBuilder application)
    {
        // No middleware needed
    }

    public int Order => 3001; // Run after core registrations
}

