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

        // Decorate INopHtmlHelper with our SEO-enhanced version
        services.AddScoped<INopHtmlHelper, SeoEnhancedNopHtmlHelper>();
    }

    public void Configure(IApplicationBuilder application)
    {
        // No middleware needed
    }

    public int Order => 3001; // Run after core registrations
}
