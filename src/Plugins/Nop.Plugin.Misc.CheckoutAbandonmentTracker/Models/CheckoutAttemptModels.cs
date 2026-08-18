using System;
using Nop.Web.Framework.Models;

namespace Nop.Plugin.Misc.CheckoutAbandonmentTracker.Models
{
    public record CheckoutAttemptSearchModel : BaseSearchModel
    {
        public bool AbandonedOnly { get; set; } = true;
    }

    public record CheckoutAttemptModel : BaseNopEntityModel
    {
        public int? CustomerId { get; set; }
        public string CustomerEmail { get; set; } = string.Empty;
        public string CustomerGuid { get; set; } = string.Empty;
        public string LastStepReached { get; set; } = string.Empty;
        public DateTime StartedOnUtc { get; set; }
        public DateTime LastActivityUtc { get; set; }
        public decimal? CartTotal { get; set; }
        public bool IsAbandoned { get; set; }
        public int? OrderId { get; set; }
    }

    public record CheckoutAttemptListModel : BasePagedListModel<CheckoutAttemptModel>;

    // Payload for the public storefront tracking endpoint (PayPal Express etc.)
    public record CheckoutTrackingEventModel : BaseNopModel
    {
        public string EventName { get; set; } = string.Empty;
    }
}

