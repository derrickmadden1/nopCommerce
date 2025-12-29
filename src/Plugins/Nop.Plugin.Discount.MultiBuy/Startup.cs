using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Infrastructure;
using Nop.Plugin.DiscountRules.MultiBuy.Services;
using Nop.Services.Discounts;
using Nop.Services.Orders;

namespace Nop.Plugin.Discount.MultiBuy
{
    public class Startup : INopStartup
    {
        public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            // register the multi-buy requirement rule and calculator service
            services.AddScoped<IDiscountRequirementRule, MultiBuyDiscountRequirement>();
            services.AddScoped<MultiBuyDiscountService>();

            // override order total calculation with multi-buy aware implementation
            services.AddScoped<IOrderTotalCalculationService, MultiBuyOrderTotalCalculationService>();
        }

        public void Configure(Microsoft.AspNetCore.Builder.IApplicationBuilder application)
        {
            // Nothing needed here
        }

        public int Order => 3000;
    }
}