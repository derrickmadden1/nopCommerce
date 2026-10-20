using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Nop.Core.Domain.Customers;
using Nop.Plugin.Widgets.AiChatbot.Services;
using Nop.Services.Events;

namespace Nop.Plugin.Widgets.AiChatbot.Infrastructure;

/// <summary>
/// Consumes CustomerLoggedinEvent to seamlessly associate guest chat sessions with the registered customer
/// </summary>
public class CustomerLoginConsumer : IConsumer<CustomerLoggedinEvent>
{
    private readonly ChatSessionService _chatSessionService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CustomerLoginConsumer(
        ChatSessionService chatSessionService,
        IHttpContextAccessor httpContextAccessor)
    {
        _chatSessionService = chatSessionService;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task HandleEventAsync(CustomerLoggedinEvent eventMessage)
    {
        if (eventMessage.Customer == null || eventMessage.Customer.Id <= 0)
            return;

        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext != null && httpContext.Request.Cookies.TryGetValue("ai_chat_session_guid", out var guidStr))
        {
            if (Guid.TryParse(guidStr, out var sessionGuid) && sessionGuid != Guid.Empty)
            {
                await _chatSessionService.AssociateSessionWithCustomerAsync(sessionGuid, eventMessage.Customer.Id);
            }
        }
    }
}
