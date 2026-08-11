using System;
using Nop.Core;

namespace Nop.Plugin.Misc.CheckoutAbandonmentTracker.Domain
{
    public class CheckoutAttempt : BaseEntity
    {
        public int? CustomerId { get; set; }
        public string CustomerGuid { get; set; } = string.Empty; // covers guest checkout
        public int StoreId { get; set; }
        public DateTime StartedOnUtc { get; set; }
        public DateTime LastActivityUtc { get; set; }
        public int LastStepReachedId { get; set; } // maps to CheckoutStep enum
        public int? OrderId { get; set; } // set on successful completion
        public bool IsAbandoned { get; set; }
        public decimal? CartTotal { get; set; }

        public CheckoutStep LastStepReached
        {
            get => (CheckoutStep)LastStepReachedId;
            set => LastStepReachedId = (int)value;
        }
    }

    public enum CheckoutStep
    {
        CartViewed = 10,
        CheckoutStarted = 20,
        BillingAddress = 30,
        ShippingAddress = 40,
        PaymentMethod = 50,
        ExpressCheckoutClicked = 55, // PayPal wallet, Card, and GPay — all render via the same SDK call
        Confirmed = 60
    }
}
