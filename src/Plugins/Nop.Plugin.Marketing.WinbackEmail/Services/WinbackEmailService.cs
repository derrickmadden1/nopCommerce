using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nop.Services.Logging;
using Nop.Services.Stores;
using Nop.Core;
using Nop.Core.Domain.Messages;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Customers;
using Nop.Plugin.Marketing.WinbackEmail.Models;
using Nop.Services.Catalog;
using Nop.Services.Customers;
using Nop.Services.Messages;
using Nop.Services.Orders;
using Nop.Services.Common;

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
    private readonly IAddressService _addressService;
    private readonly IGenericAttributeService _genericAttributeService;
    private readonly Nop.Services.Logging.ILogger _logger;

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
        IAddressService addressService,
        IGenericAttributeService genericAttributeService,
        Nop.Services.Logging.ILogger logger)
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
        _addressService = addressService;
        _genericAttributeService = genericAttributeService;
        _logger = logger;
    }

    public async Task ProcessWinbacksAsync()
    {
        await _logger.ErrorAsync($"WinbackEmail: Task triggered. Enabled={_settings.Enabled}");

        if (!_settings.Enabled)
            return;

        var emailAccount = await GetEmailAccountAsync();
        if (emailAccount == null)
        {
            await _logger.ErrorAsync("WinbackEmail: Configured FromEmail not found.");
            return;
        }

        var states = await GetWinbackStatesAsync(0);
        var dueStates = states.Where(s => s.IsDueToday).ToList();
        
        await _logger.ErrorAsync($"WinbackEmail: Found {states.Count} total states, {dueStates.Count} are due today.");

        foreach (var state in dueStates)
        {
            try
            {
                var context = await BuildContextAsync(state.Customer.Id, state.Email, state.FirstName, state.NextEmailSequence, (int)(DateTime.UtcNow - state.Order.CreatedOnUtc).TotalDays);
                var generated = await _generator.GenerateAsync(context);

                if (generated == null)
                {
                    await _logger.ErrorAsync($"WinbackEmail: Generation failed for {state.Email}");
                    continue;
                }

                await QueueEmailAsync(emailAccount, state.Email, state.FirstName, generated);

                // Update state attributes
                if (state.NextEmailSequence == 1)
                    await _genericAttributeService.SaveAttributeAsync(state.Customer, "Winback_Email1SentDateUtc", (DateTime?)DateTime.UtcNow.Date);
                else if (state.NextEmailSequence == 2)
                    await _genericAttributeService.SaveAttributeAsync(state.Customer, "Winback_Email2SentDateUtc", (DateTime?)DateTime.UtcNow.Date);
                else if (state.NextEmailSequence == 3)
                    await _genericAttributeService.SaveAttributeAsync(state.Customer, "Winback_Email3SentDateUtc", (DateTime?)DateTime.UtcNow.Date);
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync($"WinbackEmail: Error processing {state.Email}", ex);
            }
        }
    }

    public async Task<List<UpcomingEmailModel>> GetUpcomingEmailsAsync()
    {
        var result = new List<UpcomingEmailModel>();
        var states = await GetWinbackStatesAsync(0);

        foreach (var state in states)
        {
            result.Add(new UpcomingEmailModel
            {
                CustomerEmail = state.Email,
                CustomerName = state.FirstName,
                EmailSequenceNumber = state.NextEmailSequence,
                ScheduledDateUtc = state.NextEmailDateUtc
            });
        }

        return result.OrderBy(x => x.ScheduledDateUtc).ThenBy(x => x.EmailSequenceNumber).ToList();
    }

    private class WinbackCustomerState
    {
        public Customer Customer { get; set; } = null!;
        public Order Order { get; set; } = null!;
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public int NextEmailSequence { get; set; }
        public DateTime NextEmailDateUtc { get; set; }
        public bool IsDueToday { get; set; }
    }

    private async Task<List<WinbackCustomerState>> GetWinbackStatesAsync(int storeId)
    {
        var result = new List<WinbackCustomerState>();
        var maxDays = _settings.MaxDaysLapsed > 0 ? _settings.MaxDaysLapsed : 365;
        
        var fromDate = DateTime.UtcNow.Date.AddDays(-maxDays);
        var toDate = DateTime.UtcNow.Date.AddDays(-_settings.Email1DaysLapsed).AddDays(1).AddSeconds(-1);
        
        var orders = await _orderService.SearchOrdersAsync(
            storeId: storeId,
            createdFromUtc: fromDate,
            createdToUtc: toDate
        );

        var latestCustomerOrders = orders
            .GroupBy(o => o.CustomerId)
            .Select(g => g.OrderByDescending(o => o.CreatedOnUtc).First())
            .ToList();

        var today = DateTime.UtcNow.Date;

        // Collect resolved email candidates
        var candidates = new List<(Order Order, Customer Customer, string Email, string FirstName)>();

        foreach (var order in latestCustomerOrders)
        {
            var customer = await _customerService.GetCustomerByIdAsync(order.CustomerId);
            if (customer == null || customer.Deleted || !customer.Active)
                continue;

            var (email, firstName) = await GetCustomerContactInfoAsync(customer, order);
            if (string.IsNullOrEmpty(email))
                continue;

            candidates.Add((order, customer, email, firstName));
        }

        // Group by email to ensure we only process the absolute most recent order per email address
        var latestEmailCandidates = candidates
            .GroupBy(c => c.Email, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(c => c.Order.CreatedOnUtc).First())
            .ToList();

        foreach (var candidate in latestEmailCandidates)
        {
            var order = candidate.Order;
            var customer = candidate.Customer;
            var email = candidate.Email;
            var firstName = candidate.FirstName;

            // Verify there is no newer order for this specific CustomerId
            var allCustomerOrders = await _orderService.SearchOrdersAsync(customerId: order.CustomerId, storeId: storeId);
            var actualMostRecentByCustomer = allCustomerOrders.OrderByDescending(o => o.CreatedOnUtc).FirstOrDefault();
            if (actualMostRecentByCustomer != null && actualMostRecentByCustomer.Id != order.Id && actualMostRecentByCustomer.CreatedOnUtc > order.CreatedOnUtc)
                continue;

            // Verify there is no newer order that used this email as the billing email (e.g. a recent guest checkout)
            var ordersByBillingEmail = await _orderService.SearchOrdersAsync(billingEmail: email, storeId: storeId);
            var actualMostRecentByBilling = ordersByBillingEmail.OrderByDescending(o => o.CreatedOnUtc).FirstOrDefault();
            if (actualMostRecentByBilling != null && actualMostRecentByBilling.Id != order.Id && actualMostRecentByBilling.CreatedOnUtc > order.CreatedOnUtc)
                continue;

            var subscriptions = await _newsletterService.GetNewsLetterSubscriptionsByEmailAsync(email, storeId: storeId);
            var subscription = subscriptions.FirstOrDefault();
            if (subscription != null && subscription.Active == false)
                continue;

            var lastOrderId = await _genericAttributeService.GetAttributeAsync<int>(customer, "Winback_LastOrderId");
            if (lastOrderId != order.Id)
            {
                await _genericAttributeService.SaveAttributeAsync(customer, "Winback_Email1SentDateUtc", (DateTime?)null);
                await _genericAttributeService.SaveAttributeAsync(customer, "Winback_Email2SentDateUtc", (DateTime?)null);
                await _genericAttributeService.SaveAttributeAsync(customer, "Winback_Email3SentDateUtc", (DateTime?)null);
                await _genericAttributeService.SaveAttributeAsync(customer, "Winback_LastOrderId", order.Id);
            }

            var email1Sent = await _genericAttributeService.GetAttributeAsync<DateTime?>(customer, "Winback_Email1SentDateUtc");
            var email2Sent = await _genericAttributeService.GetAttributeAsync<DateTime?>(customer, "Winback_Email2SentDateUtc");
            var email3Sent = await _genericAttributeService.GetAttributeAsync<DateTime?>(customer, "Winback_Email3SentDateUtc");

            int nextSeq = 0;
            DateTime nextDate = DateTime.MinValue;

            if (email1Sent == null)
            {
                nextSeq = 1;
                var eligibleDate = order.CreatedOnUtc.Date.AddDays(_settings.Email1DaysLapsed);
                nextDate = eligibleDate < today ? today : eligibleDate; 
            }
            else if (email2Sent == null)
            {
                nextSeq = 2;
                nextDate = email1Sent.Value.Date.AddDays(_settings.DaysBetweenEmail1And2);
            }
            else if (email3Sent == null)
            {
                nextSeq = 3;
                nextDate = email2Sent.Value.Date.AddDays(_settings.DaysBetweenEmail2And3);
            }
            else
            {
                continue; // Sequence complete
            }

            result.Add(new WinbackCustomerState
            {
                Customer = customer,
                Order = order,
                Email = email,
                FirstName = firstName,
                NextEmailSequence = nextSeq,
                NextEmailDateUtc = nextDate,
                IsDueToday = nextDate.Date <= today
            });
        }

        return result;
    }

    private async Task<(string Email, string FirstName)> GetCustomerContactInfoAsync(Customer customer, Order order)
    {
        var email = customer.Email;
        var firstName = await _customerService.GetCustomerFullNameAsync(customer);
        firstName = firstName?.Split(' ').FirstOrDefault();

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(firstName))
        {
            var billingAddress = await _addressService.GetAddressByIdAsync(order.BillingAddressId);
            if (billingAddress != null)
            {
                if (string.IsNullOrEmpty(email)) email = billingAddress.Email;
                if (string.IsNullOrEmpty(firstName)) firstName = billingAddress.FirstName;
            }
        }
        
        return (email, firstName ?? "there");
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
        // Secondary safety net for Dry Run: invalidate the email address
        if (_settings.DryRun)
        {
            toEmail = $"{toEmail}.test";
        }

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
            EmailAccountId = emailAccount.Id,
            DontSendBeforeDateUtc = _settings.DryRun ? DateTime.UtcNow.AddYears(100) : null
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
