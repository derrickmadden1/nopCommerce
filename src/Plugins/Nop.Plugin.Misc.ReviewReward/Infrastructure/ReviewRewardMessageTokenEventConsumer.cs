using System;
using System.Threading.Tasks;
using Nop.Core.Domain.Messages;
using Nop.Plugin.Misc.ReviewReward.Services;
using Nop.Services.Events;

namespace Nop.Plugin.Misc.ReviewReward.Infrastructure
{
    public class ReviewRewardMessageTokenEventConsumer : IConsumer<AdditionalTokensAddedEvent>
    {
        public Task HandleEventAsync(AdditionalTokensAddedEvent eventMessage)
        {
            if (eventMessage?.MessageTemplate == null || string.IsNullOrEmpty(eventMessage.MessageTemplate.Name))
                return Task.CompletedTask;

            if (eventMessage.MessageTemplate.Name.Equals(ReviewRewardMessageService.MessageTemplateName, StringComparison.InvariantCultureIgnoreCase))
            {
                eventMessage.AddTokens(
                    "%ReviewReward.CouponCode%",
                    "%ReviewReward.RewardAmount%",
                    "%ReviewReward.ProductName%",
                    "%ReviewReward.ExpiryDate%"
                );
            }

            return Task.CompletedTask;
        }
    }
}
