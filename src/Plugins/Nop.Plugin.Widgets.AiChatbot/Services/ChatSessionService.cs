using System.Text.Json;
using Nop.Data;
using Nop.Core;
using Nop.Plugin.Widgets.AiChatbot.Domain;
using Nop.Plugin.Widgets.AiChatbot.Models;
using Nop.Services.Customers;

namespace Nop.Plugin.Widgets.AiChatbot.Services;

public class ChatSessionService
{
    private readonly IRepository<ChatSession> _chatSessionRepository;
    private readonly ICustomerService _customerService;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ChatSessionService(
        IRepository<ChatSession> chatSessionRepository,
        ICustomerService customerService)
    {
        _chatSessionRepository = chatSessionRepository;
        _customerService = customerService;
    }

    public async Task<ChatSession> GetOrCreateSessionAsync(Guid? sessionGuid, int customerId)
    {
        ChatSession? session = null;

        if (sessionGuid.HasValue && sessionGuid.Value != Guid.Empty)
        {
            session = await _chatSessionRepository.Table
                .FirstOrDefaultAsync(s => s.SessionGuid == sessionGuid.Value);
        }

        if (session == null && customerId > 0)
        {
            // Fallback to customer's most recent session
            session = await _chatSessionRepository.Table
                .Where(s => s.CustomerId == customerId)
                .OrderByDescending(s => s.UpdatedOnUtc)
                .FirstOrDefaultAsync();
        }

        if (session == null)
        {
            var now = DateTime.UtcNow;
            session = new ChatSession
            {
                SessionGuid = sessionGuid.HasValue && sessionGuid.Value != Guid.Empty ? sessionGuid.Value : Guid.NewGuid(),
                CustomerId = customerId,
                MessagesJson = "[]",
                CreatedOnUtc = now,
                UpdatedOnUtc = now
            };

            await _chatSessionRepository.InsertAsync(session);
        }
        else if (session.CustomerId == 0 && customerId > 0)
        {
            // Associate guest session with newly logged in customer
            session.CustomerId = customerId;
            await _chatSessionRepository.UpdateAsync(session);
        }

        return session;
    }

    public async Task<ChatSession> SaveTurnAsync(Guid sessionGuid, int customerId, string userMessage, string assistantReply)
    {
        var session = await GetOrCreateSessionAsync(sessionGuid, customerId);

        List<ConversationTurn> turns;
        try
        {
            turns = JsonSerializer.Deserialize<List<ConversationTurn>>(session.MessagesJson, JsonOptions)
                    ?? new List<ConversationTurn>();
        }
        catch
        {
            turns = new List<ConversationTurn>();
        }

        turns.Add(new ConversationTurn { Role = "user", Content = userMessage });
        turns.Add(new ConversationTurn { Role = "assistant", Content = assistantReply });

        session.MessagesJson = JsonSerializer.Serialize(turns, JsonOptions);
        session.UpdatedOnUtc = DateTime.UtcNow;

        await _chatSessionRepository.UpdateAsync(session);

        return session;
    }

    public async Task<IPagedList<ChatSessionItemModel>> GetPagedSessionsAsync(int pageIndex = 0, int pageSize = 20)
    {
        var query = _chatSessionRepository.Table
            .OrderByDescending(s => s.UpdatedOnUtc);

        var pagedSessions = await query.ToPagedListAsync(pageIndex, pageSize);

        var items = new List<ChatSessionItemModel>();
        foreach (var session in pagedSessions)
        {
            var turnsCount = 0;
            try
            {
                var turns = JsonSerializer.Deserialize<List<ConversationTurn>>(session.MessagesJson, JsonOptions);
                turnsCount = turns?.Count ?? 0;
            }
            catch { }

            string customerName = "Guest";
            string customerEmail = "-";

            if (session.CustomerId > 0)
            {
                var customer = await _customerService.GetCustomerByIdAsync(session.CustomerId);
                if (customer != null)
                {
                    customerName = await _customerService.GetCustomerFullNameAsync(customer);
                    if (string.IsNullOrWhiteSpace(customerName))
                        customerName = customer.Username ?? $"Customer #{customer.Id}";
                    customerEmail = customer.Email ?? "-";
                }
            }

            items.Add(new ChatSessionItemModel
            {
                Id = session.Id,
                SessionGuid = session.SessionGuid,
                CustomerId = session.CustomerId,
                CustomerName = customerName,
                CustomerEmail = customerEmail,
                MessageCount = turnsCount,
                CreatedOnUtc = session.CreatedOnUtc,
                UpdatedOnUtc = session.UpdatedOnUtc
            });
        }

        return new PagedList<ChatSessionItemModel>(items, pageIndex, pageSize, pagedSessions.TotalCount);
    }

    public async Task<ChatSessionDetailsModel?> GetSessionDetailsAsync(int id)
    {
        var session = await _chatSessionRepository.GetByIdAsync(id);
        if (session == null)
            return null;

        var turns = new List<ConversationTurn>();
        try
        {
            turns = JsonSerializer.Deserialize<List<ConversationTurn>>(session.MessagesJson, JsonOptions)
                    ?? new List<ConversationTurn>();
        }
        catch { }

        string customerName = "Guest";
        string customerEmail = "-";

        if (session.CustomerId > 0)
        {
            var customer = await _customerService.GetCustomerByIdAsync(session.CustomerId);
            if (customer != null)
            {
                customerName = await _customerService.GetCustomerFullNameAsync(customer);
                if (string.IsNullOrWhiteSpace(customerName))
                    customerName = customer.Username ?? $"Customer #{customer.Id}";
                customerEmail = customer.Email ?? "-";
            }
        }

        return new ChatSessionDetailsModel
        {
            Id = session.Id,
            SessionGuid = session.SessionGuid,
            CustomerId = session.CustomerId,
            CustomerName = customerName,
            CustomerEmail = customerEmail,
            CreatedOnUtc = session.CreatedOnUtc,
            UpdatedOnUtc = session.UpdatedOnUtc,
            Turns = turns
        };
    }

    public async Task DeleteSessionAsync(int id)
    {
        var session = await _chatSessionRepository.GetByIdAsync(id);
        if (session != null)
        {
            await _chatSessionRepository.DeleteAsync(session);
        }
    }
}
