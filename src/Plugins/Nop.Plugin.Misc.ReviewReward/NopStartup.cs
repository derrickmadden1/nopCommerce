using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Infrastructure;
using Nop.Plugin.Misc.ReviewReward.Discounts;
using Nop.Plugin.Misc.ReviewReward.Infrastructure;
using Nop.Plugin.Misc.ReviewReward.Services;

namespace Nop.Plugin.Misc.ReviewReward
{
    public class NopStartup : INopStartup
    {
        public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            services.AddScoped<IReviewRewardService, ReviewRewardService>();
            services.AddScoped<IReviewRewardMessageService, ReviewRewardMessageService>();
            services.AddScoped<ReviewRewardRequirementRule>();
            services.AddScoped<ReviewRewardCartActionFilter>();

            services.Configure<MvcOptions>(options =>
            {
                options.Filters.AddService<ReviewRewardCartActionFilter>();
            });
        }

        public void Configure(IApplicationBuilder application)
        {
        }

        public int Order => 3000;
    }
}
