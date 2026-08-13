using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Messages;
using Nop.Core.Events;
using Nop.Core.Infrastructure;
using Nop.Plugin.Misc.ReviewReward.Infrastructure;
using Nop.Plugin.Misc.ReviewReward.Services;
using Nop.Services.Events;
using Nop.Web.Framework.Events;

namespace Nop.Plugin.Misc.ReviewReward
{
    public class NopStartup : INopStartup
    {
        public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            services.AddScoped<IReviewRewardService, ReviewRewardService>();
            services.AddScoped<IReviewRewardMessageService, ReviewRewardMessageService>();
            services.AddScoped<IConsumer<EntityInsertedEvent<ProductReview>>, ProductReviewEventConsumer>();
            services.AddScoped<IConsumer<ProductReviewApprovedEvent>, ProductReviewEventConsumer>();
            services.AddScoped<IConsumer<AdditionalTokensAddedEvent>, ReviewRewardMessageTokenEventConsumer>();
            services.AddScoped<IConsumer<AdminMenuCreatedEvent>, AdminMenuConsumer>();
        }

        public void Configure(IApplicationBuilder application)
        {
        }

        public int Order => 3000;
    }
}
