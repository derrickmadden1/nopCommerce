using System.Threading.Tasks;
using Nop.Services.Events;
using Nop.Web.Framework.Events;
using Nop.Web.Framework.Menu;

namespace Nop.Plugin.Misc.ReviewReward.Infrastructure
{
    public class AdminMenuConsumer : IConsumer<AdminMenuCreatedEvent>
    {
        public Task HandleEventAsync(AdminMenuCreatedEvent eventMessage)
        {
            if (eventMessage?.RootMenuItem == null)
                return Task.CompletedTask;

            var promotionsMenu = eventMessage.RootMenuItem.GetItemBySystemName("Promotions");
            var parentMenu = promotionsMenu ?? eventMessage.RootMenuItem;

            parentMenu.InsertAfter("Discounts", new AdminMenuItem
            {
                SystemName = "Misc.ReviewReward",
                Title = "Review Rewards",
                Url = eventMessage.GetMenuItemUrl("ReviewRewardAdmin", "Configure"),
                IconClass = "fas fa-award",
                Visible = true
            });

            return Task.CompletedTask;
        }
    }
}
