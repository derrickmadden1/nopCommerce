using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Nop.Core.Domain.Orders;
using Nop.Services.Payments;
using Nop.Services.Plugins;
using Nop.Core.Domain.Payments;

namespace Nop.Plugin.Misc.UniversalCommerce
{
    public class AgentUniversalPaymentProcessor : BasePlugin, IPaymentMethod
    {
        // Tell the admin configuration system where your entry page button points
        public override string GetConfigurationPageUrl()
        {
            return "/Admin/UcpAdmin/Configure";
        }

        #region IPaymentMethod Implementation

        /// <summary>
        /// Processes the payment programmatically when the checkout API calls PlaceOrderAsync
        /// </summary>
        public Task<ProcessPaymentResult> ProcessPaymentAsync(ProcessPaymentRequest processPaymentRequest)
        {
            var result = new ProcessPaymentResult();

            // Extract the cryptographic token passed from your API controller
            if (!processPaymentRequest.CustomValues.TryGetValue("Ap2Token", out var ap2TokenObj) ||
                string.IsNullOrEmpty(ap2TokenObj?.ToString()))
            {
                result.AddError("Google AP2 Payment Token is missing from the checkout payload.");
                return Task.FromResult(result);
            }

            // In production, your token validation service should verify Google's signature here.
            // If valid, immediately complete the transaction state.
            result.NewPaymentStatus = PaymentStatus.Authorized;

            // Store the token or reference ID directly on the order record for future reference
            result.AuthorizationTransactionId = $"AP2-{Guid.NewGuid():N}";

            return Task.FromResult(result);
        }

        /// <summary>
        /// Post-payment processing (Used for standard browser redirects, skip for headless/agent transactions)
        /// </summary>
        public Task PostProcessPaymentAsync(PostProcessPaymentRequest postProcessPaymentRequest)
        {
            // Do nothing as the transaction completes entirely on the initial API call
            return Task.CompletedTask;
        }

        public Task<bool> HidePaymentMethodAsync(IList<ShoppingCartItem> cart)
        {
            return Task.FromResult(false);
        }

        public Task<decimal> GetAdditionalHandlingFeeAsync(IList<ShoppingCartItem> cart)
        {
            return Task.FromResult(0m);
        }

        public Task<ProcessPaymentResult> ProcessRecurringPaymentAsync(ProcessPaymentRequest processPaymentRequest)
        {
            var result = new ProcessPaymentResult();
            result.AddError("Recurring payment not supported");
            return Task.FromResult(result);
        }

        public Task<CancelRecurringPaymentResult> CancelRecurringPaymentAsync(CancelRecurringPaymentRequest cancelPaymentRequest)
        {
            var result = new CancelRecurringPaymentResult();
            result.AddError("Recurring payment not supported");
            return Task.FromResult(result);
        }

        public Task<bool> CanRePostProcessPaymentAsync(Order order)
        {
            return Task.FromResult(false);
        }

        public Task<IList<string>> ValidatePaymentFormAsync(IFormCollection form)
        {
            return Task.FromResult<IList<string>>(new List<string>());
        }

        public Task<ProcessPaymentRequest> GetPaymentInfoAsync(IFormCollection form)
        {
            return Task.FromResult(new ProcessPaymentRequest());
        }

        /// <summary>
        /// Captures an authorized transaction (Called during shipping or automated settlement)
        /// </summary>
        public Task<CapturePaymentResult> CaptureAsync(CapturePaymentRequest capturePaymentRequest)
        {
            var result = new CapturePaymentResult
            {
                NewPaymentStatus = PaymentStatus.Paid,
                CaptureTransactionId = capturePaymentRequest.Order.AuthorizationTransactionId
            };
            return Task.FromResult(result);
        }

        public Task<VoidPaymentResult> VoidAsync(VoidPaymentRequest voidPaymentRequest)
        {
            return Task.FromResult(new VoidPaymentResult { NewPaymentStatus = PaymentStatus.Voided });
        }

        public Task<RefundPaymentResult> RefundAsync(RefundPaymentRequest refundPaymentRequest)
        {
            return Task.FromResult(new RefundPaymentResult { NewPaymentStatus = PaymentStatus.Refunded });
        }

        #endregion

        #region Properties & Configurations

        // Set to true so nopCommerce allows capturing authorized funds
        public bool SupportCapture => true;
        public bool SupportPartiallyRefund => false;
        public bool SupportRefund => true;
        public bool SupportVoid => true;
        public RecurringPaymentType RecurringPaymentType => RecurringPaymentType.NotSupported;
        public PaymentMethodType PaymentMethodType => PaymentMethodType.Standard;
        public bool SkipPaymentInfo => true;

        public Task<string> GetPaymentMethodDescriptionAsync()
        {
            return Task.FromResult("Processes headless orders via Google Agent Payments Protocol (AP2).");
        }

        // Must match the system name used in your controller mapping exactly
        public string PaymentMethodSystemName => "Payments.AgentUniversal";

        #endregion

        #region View Components (Skip UI Rendering)

        public Type GetPublicViewComponent()
        {
            return null; // Headless integration, no UI rendered
        }

        #endregion
    }
}
