using Microsoft.Playwright;

namespace Nop.Tests.E2E.Flows;

public enum CheckoutAuthMode
{
    Guest,
    ReturningCustomer,
    NewCustomerRegistration
}

public enum PayPalFundingSource
{
    PayPal,
    PayLater,
    Card
}

public record CheckoutResult(
    string RouteName,
    bool Success,
    string? OrderNumber,
    TimeSpan Duration,
    string? ErrorMessage);

public interface ICheckoutFlow
{
    string RouteName { get; }
    Task<CheckoutResult> RunAsync(IPage page, CancellationToken cancellationToken = default);
}

