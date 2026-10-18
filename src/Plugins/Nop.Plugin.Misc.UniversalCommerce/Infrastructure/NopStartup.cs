using System;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Hybrid; // Required for .NET 10 HybridCache config
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Infrastructure;
using Nop.Plugin.Misc.UniversalCommerce.Domain;
using Nop.Plugin.Misc.UniversalCommerce.Services;

namespace Nop.Plugin.Misc.UniversalCommerce.Infrastructure
{
    public class NopStartup : INopStartup
    {
        public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            // 1. Register HttpClient required by your public key fetching routines
            services.AddHttpClient();

            // 2. Register .NET 10 HybridCache with a safe global maximum payload size
#pragma warning disable EXTEXP0018 // HybridCache is marked preview/experimental in some early .NET 10 builds
            services.AddHybridCache(options =>
            {
                // Prevent malicious agents from flooding cache blocks with massive keys
                options.MaximumPayloadBytes = 1024 * 1024; // 1MB max cache entry sizing
                options.DefaultEntryOptions = new HybridCacheEntryOptions
                {
                    Expiration = TimeSpan.FromHours(24) // Optimal duration for Google signature keys
                    // LocalCacheExpiration is not strictly necessary or might be named differently
                };
            });
#pragma warning restore EXTEXP0018

            // 3. Register your cryptographic token validator
            services.AddScoped<IAp2SecurityService, Ap2SecurityService>();

            // 4. Set up rate-limiting rules using your custom UcpSettings records dynamically per request
            services.AddRateLimiter(options =>
            {
                options.AddPolicy("UcpAgentPolicy", new UcpAgentRateLimiterPolicy());
            });
        }

        public void Configure(IApplicationBuilder application)
        {
            // nopCommerce natively injects application.UseRateLimiter() via Nop.Web.Framework,
            // so we do not need to register it again here.
        }

        // Must execute high enough in the stack sequence to handle requests before standard UI routing catches them
        public int Order => 400;
    }
}
