using System.Diagnostics;
using Microsoft.Playwright;
using Nop.Tests.E2E.Configuration;
using NUnit.Framework;

namespace Nop.Tests.E2E.Flows;

public class SquareSandboxCheckoutFlow : BaseCheckoutFlow
{
    public CheckoutAuthMode AuthMode { get; }
    public bool SaveCard { get; }
    public bool UseStoredCard { get; }
    public string? CustomCardNumber { get; }
    public bool ExpectPaymentFailure { get; }

    public override string RouteName
    {
        get
        {
            var mode = AuthMode.ToString().ToLowerInvariant();
            if (UseStoredCard) return $"square-sandbox-checkout-{mode}-stored-card";
            if (SaveCard) return $"square-sandbox-checkout-{mode}-save-card";
            if (!string.IsNullOrEmpty(CustomCardNumber)) return $"square-sandbox-checkout-{mode}-custom-card";
            return $"square-sandbox-checkout-{mode}";
        }
    }

    public SquareSandboxCheckoutFlow(
        CheckoutTestSettings? settings = null,
        CheckoutAuthMode authMode = CheckoutAuthMode.Guest,
        bool saveCard = false,
        bool useStoredCard = false,
        string? customCardNumber = null,
        bool expectPaymentFailure = false) 
        : base(settings)
    {
        AuthMode = authMode;
        SaveCard = saveCard;
        UseStoredCard = useStoredCard;
        CustomCardNumber = customCardNumber;
        ExpectPaymentFailure = expectPaymentFailure;
    }

    public override async Task<CheckoutResult> RunAsync(IPage page, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // 1. Add item to cart
            await AddTestProductToCartAsync(page);

            // 2. Go to cart and accept terms
            await ProceedToCartAndAcceptTermsAsync(page);

            // 3. Handle authentication step (Guest, Returning Customer, or New Registration)
            await HandleAuthenticationStepAsync(page, AuthMode);

            // 4. Fill Billing & Shipping addresses
            await FillBillingAddressIfRequiredAsync(page);
            await FillShippingAddressIfRequiredAsync(page);

            // 5. Select Shipping method
            await SelectShippingMethodAsync(page);

            // 6. Select Square Payment Method
            await SelectPaymentMethodAsync(page, "Payments.Square");

            // 7. Fill Square Payment Info (Web Payments SDK iframe or Stored Card)
            await FillSquarePaymentDetailsAsync(page, saveCard: SaveCard, useStoredCard: UseStoredCard, customCardNumber: CustomCardNumber);

            // 8. Confirm Order
            await ConfirmOrderAsync(page);

            if (ExpectPaymentFailure)
            {
                var failureMessage = await WaitForPaymentFailureMessageAsync(page);
                stopwatch.Stop();
                return new CheckoutResult(
                    RouteName,
                    Success: true, // Successfully encountered expected failure
                    OrderNumber: null,
                    Duration: stopwatch.Elapsed,
                    ErrorMessage: failureMessage);
            }

            // 9. Assert order completed
            var orderNumber = await WaitForOrderCompletionAndGetNumberAsync(page);
            stopwatch.Stop();

            return new CheckoutResult(
                RouteName,
                Success: true,
                OrderNumber: orderNumber,
                Duration: stopwatch.Elapsed,
                ErrorMessage: null);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new CheckoutResult(
                RouteName,
                Success: false,
                OrderNumber: null,
                Duration: stopwatch.Elapsed,
                ErrorMessage: ex.Message);
        }
    }
}
