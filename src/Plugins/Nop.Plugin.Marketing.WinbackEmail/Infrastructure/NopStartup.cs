using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Infrastructure;
using Nop.Plugin.Marketing.WinbackEmail.Services;

namespace Nop.Plugin.Marketing.WinbackEmail.Infrastructure;

public class NopStartup : INopStartup
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<WinbackEmailGenerator>();
        services.AddScoped<WinbackEmailService>();
    }

    public void Configure(IApplicationBuilder app) { }

    public int Order => 100;
}
