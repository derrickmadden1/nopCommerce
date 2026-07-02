using Microsoft.Extensions.Logging;
using Nop.Core;
using Nop.Core.Domain.Messages;
using Nop.Core.Domain.Orders;
using Nop.Plugin.Marketing.WinbackEmail.Models;
using Nop.Services.Catalog;
using Nop.Services.Customers;
using Nop.Services.Messages;
using Nop.Services.Orders;

namespace Nop.Plugin.Marketing.WinbackEmail.Services;

public class WinbackEmailService
{
    private readonly WinbackEmailSettings _settings;
    private readonly WinbackEmailGenerator _generator;
    private readonly ICustomerService _customerService;
    private readonly IOrderService _orderService;
    private readonly IProductService _productService;
    private readonly IEmailAccountService _emailAccountService;
    private readonly IQueuedEmailService _queuedEmailService;
    private readonly INewsLetterSubscriptionService _newsletterService;
    private readonly IStoreContext _storeContext;
    private readonly ILogger<WinbackEmailService> _logger;

    public WinbackEmailService(
        WinbackEmailSettings settings,
        WinbackEmailGenerator generator,
        ICustomerService customerService,
        IOrderService orderService,
        IProductService productService,
        IEmailAccountService emailAccountService,
        IQueuedEmailService queuedEmailService,
        INewsLetterSubscriptionService newsletterService,
        IStoreContext storeContext,
        ILogger<WinbackEmailService> logger)
    {
        _settings = settings;
        _generator = generator;
        _customerService = customerService;
        _orderService = orderService;
        _productService = productService;
        _emailAccountService = emailAccountService;
        _queuedEmailService = queuedEmailService;
        _newsletterService = newsletterService;
        _storeContext = storeContext;
        _logger = logger;
    }

    /// <summary>
    /// Called by the scheduled task — finds lapsed customers and queues winback emails
    /// </summary>
    public async Task ProcessWinbacksAsync()
    {
        if (!_settings.Enabled)
            return;

        var store = await _storeContext.GetCurrentStoreAsync();
        var emailAccount = await GetEmailAccountAsync();

        if (emailAccount == null)
        {
            _logger.LogWarning("Winback: No matching email account found for {FromEmail}", _settings.FromEmail);
            return;
        }

        // Process each email in the sequence
        await ProcessEmailStepAsync(emailAccount, store.Id, _settings.Email1DaysLapsed, 1);
        await ProcessEmailStepAsync(emailAccount, store.Id, _settings.Email2DaysLapsed, 2);
        await ProcessEmailStepAsync(emailAccount, store.Id, _settings.Email3DaysLapsed, 3);
    }

    private async Task ProcessEmailStepAsync(EmailAccount emailAccount, int storeId, int daysLapsed, int emailNumber)
    {
        var targetDate = DateTime.UtcNow.Date.AddDays(-daysLapsed);

        // Find customers whose most recent order was exactly on targetDate
        // and who are subscribed to marketing emails
        var customers = await GetLapsedCustomersAsync(targetDate, storeId);

        _logger.LogInformation("Winback email {EmailNumber}: found {Count} customers lapsed on {Date}",
            emailNumber, customers.Count, targetDate.ToShortDateString());

        foreach (var (customerId, customerEmail, firstName) in customers)
        {
            try
            {
                var context = await BuildContextAsync(customerId, customerEmail, firstName, emailNumber, daysLapsed);
                var generated = await _generator.GenerateAsync(context);

                if (generated == null)
                {
                    _logger.LogWarning("Winback: Failed to generate email for customer {CustomerId}", customerId);
                    continue;
                }

                await QueueEmailAsync(emailAccount, customerEmail, firstName, generated);

                _logger.LogInformation("Winback email {EmailNumber} queued for {Email}", emailNumber, customerEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Winback: Error processing customer {CustomerId}", customerId);
            }
        }
    }

    private async Task<List<(int CustomerId, string Email, string FirstName)>> GetLapsedCustomersAsync(
        DateTime lastOrderDate, int storeId)
    {
        var result = new List<(int, string, string)>();

        // Get orders placed on the target date
        var orders = await _orderService.SearchOrdersAsync(
            storeId: storeId,
            createdFromUtc: lastOrderDate,
            createdToUtc: lastOrderDate.AddDays(1).AddSeconds(-1)
        );

        foreach (var order in orders)
        {
            // Check this is the customer's most recent order
            var allOrders = await _orderService.SearchOrdersAsync(
                customerId: order.CustomerId,
                storeId: storeId
            );

            var mostRecent = allOrders.OrderByDescending(o => o.CreatedOnUtc).FirstOrDefault();
            if (mostRecent?.Id != order.Id)
                continue; // They've ordered since — skip

            var customer = await _customerService.GetCustomerByIdAsync(order.CustomerId);
            if (customer == null || customer.Deleted || !customer.Active)
                continue;

            // GDPR — only send to marketing opt-in customers
            var subscriptions = await _newsletterService.GetNewsLetterSubscriptionsByEmailAsync(
                customer.Email, storeId);
            var subscription = subscriptions.FirstOrDefault();

            if (subscription == null || subscription.Active == false)
                continue;

            var firstName = await _customerService.GetCustomerFullNameAsync(customer);
            firstName = firstName?.Split(' ').FirstOrDefault() ?? "there";

            result.Add((customer.Id, customer.Email, firstName));
        }

        return result;
    }

    private async Task<WinbackCustomerContext> BuildContextAsync(
        int customerId, string email, string firstName, int emailNumber, int daysLapsed)
    {
        var orders = await _orderService.SearchOrdersAsync(customerId: customerId);
        var recentOrders = orders
            .OrderByDescending(o => o.CreatedOnUtc)
            .Take(3)
            .ToList();

        var orderSummaries = new List<OrderSummary>();

        foreach (var order in recentOrders)
        {
            var items = await _orderService.GetOrderItemsAsync(order.Id);
            var productNames = new List<string>();

            foreach (var item in items)
            {
                var product = await _productService.GetProductByIdAsync(item.ProductId);
                if (product != null)
                    productNames.Add(product.Name);
            }

            orderSummaries.Add(new OrderSummary
            {
                OrderDate = order.CreatedOnUtc,
                OrderTotal = order.OrderTotal,
                ProductNames = productNames
            });
        }

        return new WinbackCustomerContext
        {
            CustomerFirstName = firstName,
            CustomerEmail = email,
            EmailNumber = emailNumber,
            DaysSinceLastOrder = daysLapsed,
            RecentOrders = orderSummaries,
            DiscountCode = emailNumber == 3 ? _settings.Email3DiscountCode : null,
            StoreName = _settings.StoreName
        };
    }

    private async Task QueueEmailAsync(
        EmailAccount emailAccount,
        string toEmail,
        string toName,
        GeneratedEmail generated)
    {
        var queuedEmail = new QueuedEmail
        {
            Priority = QueuedEmailPriority.High,
            From = _settings.FromEmail,
            FromName = _settings.FromName,
            To = toEmail,
            ToName = toName,
            Subject = generated.Subject,
            Body = generated.HtmlBody,
            CreatedOnUtc = DateTime.UtcNow,
            EmailAccountId = emailAccount.Id
        };

        await _queuedEmailService.InsertQueuedEmailAsync(queuedEmail);
    }

    private async Task<EmailAccount?> GetEmailAccountAsync()
    {
        var accounts = await _emailAccountService.GetAllEmailAccountsAsync();
        return accounts.FirstOrDefault(a =>
            a.Email.Equals(_settings.FromEmail, StringComparison.OrdinalIgnoreCase));
    }
}
