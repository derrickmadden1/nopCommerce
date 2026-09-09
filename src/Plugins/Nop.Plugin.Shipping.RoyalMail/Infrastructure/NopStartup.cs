using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Infrastructure;
using Nop.Plugin.Shipping.RoyalMail.Services;

namespace Nop.Plugin.Shipping.RoyalMail.Infrastructure;

/// <summary>
/// Represents object for configuring services on application startup
/// </summary>
public class NopStartup : INopStartup
{
    /// <summary>
    /// Add and configure any of the middleware
    /// </summary>
    public virtual void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // Register HttpClient and RoyalMailService
        services.AddHttpClient<RoyalMailService>();
    }

    /// <summary>
    /// Configure the using of added middleware
    /// </summary>
    public void Configure(IApplicationBuilder application)
    {
    }

    /// <summary>
    /// Gets order of this startup configuration implementation
    /// </summary>
    public int Order => 3000;
}
