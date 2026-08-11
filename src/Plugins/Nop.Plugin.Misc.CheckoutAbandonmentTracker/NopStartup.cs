using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Infrastructure;
using Nop.Plugin.Misc.CheckoutAbandonmentTracker.Infrastructure;
using Nop.Plugin.Misc.CheckoutAbandonmentTracker.Services;

namespace Nop.Plugin.Misc.CheckoutAbandonmentTracker
{
    public class NopStartup : INopStartup
    {
        public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            services.AddScoped<ICheckoutAttemptService, CheckoutAttemptService>();
            services.AddScoped<CheckoutAttemptFilter>();

            // Global filter, gated by controller/action check inside the filter
            // itself, so core ShoppingCartController/CheckoutController files
            // never need to be touched directly.
            services.Configure<MvcOptions>(options =>
            {
                options.Filters.Add<CheckoutAttemptFilter>();
            });
        }

        public void Configure(IApplicationBuilder application)
        {
        }

        // Runs after core services are registered
        public int Order => 2000;
    }
}
