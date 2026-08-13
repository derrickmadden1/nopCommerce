using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Infrastructure;
using Nop.Plugin.Misc.ReviewReward.Discounts;
using Nop.Plugin.Misc.ReviewReward.Services;
using Nop.Services.Discounts;

namespace Nop.Plugin.Misc.ReviewReward
{
    public class NopStartup : INopStartup
    {
        public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            services.AddScoped<IReviewRewardService, ReviewRewardService>();
            services.AddScoped<IDiscountRequirementRule, ReviewRewardRequirementRule>();
        }

        public void Configure(IApplicationBuilder application)
        {
        }

        public int Order => 3000;
    }
}
