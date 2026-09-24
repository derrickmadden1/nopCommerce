using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Nop.Services.Configuration;
using Nop.Plugin.Widgets.AgentSearch.Services;

namespace Nop.Plugin.Widgets.AgentSearch.Infrastructure
{
    /// <summary>
    /// Action filter attribute to authenticate third-party AI agents via multi-tenant API key or legacy static key.
    /// Supports X-Api-Key header and Authorization: Bearer <key>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class AgentApiKeyAuthAttribute : TypeFilterAttribute
    {
        public AgentApiKeyAuthAttribute(string scope = "Search.Read") : base(typeof(AgentApiKeyAuthFilter))
        {
            Arguments = new object[] { scope };
        }
    }

    public class AgentApiKeyAuthFilter : IAsyncActionFilter
    {
        private readonly string _requiredScope;
        private readonly ISettingService _settingService;
        private readonly IAgentKeyService _agentKeyService;

        public AgentApiKeyAuthFilter(
            string scope,
            ISettingService settingService,
            IAgentKeyService agentKeyService)
        {
            _requiredScope = scope;
            _settingService = settingService;
            _agentKeyService = agentKeyService;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var settings = await _settingService.LoadSettingAsync<AgentSearchSettings>();

            // Master switch check
            if (!settings.Enabled)
            {
                context.Result = new NotFoundResult();
                return;
            }

            // If API key is not required by merchant configuration, allow request
            if (!settings.RequireApiKey)
            {
                await next();
                return;
            }

            // Extract key from HTTP headers
            var httpContext = context.HttpContext;
            string? providedKey = null;

            if (httpContext.Request.Headers.TryGetValue("X-Api-Key", out var headerKey) && !string.IsNullOrWhiteSpace(headerKey))
            {
                providedKey = headerKey.ToString().Trim();
            }
            else if (httpContext.Request.Headers.TryGetValue("Authorization", out var authHeader) && !string.IsNullOrWhiteSpace(authHeader))
            {
                var authStr = authHeader.ToString().Trim();
                if (authStr.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    providedKey = authStr["Bearer ".Length..].Trim();
                }
            }

            if (string.IsNullOrEmpty(providedKey))
            {
                context.Result = new UnauthorizedObjectResult(new
                {
                    error = "unauthorized",
                    detail = "Missing X-Api-Key header or Authorization: Bearer token."
                });
                return;
            }

            // 1. Validate multi-tenant key repository
            var validatedKey = await _agentKeyService.ValidateKeyAsync(providedKey, _requiredScope);
            if (validatedKey != null)
            {
                httpContext.Items["AgentApiKey"] = validatedKey;
                await next();
                return;
            }

            // 2. Legacy fallback check against single static settings key
            if (!string.IsNullOrWhiteSpace(settings.ApiKey) && providedKey == settings.ApiKey)
            {
                await next();
                return;
            }

            // Authentication failed
            context.Result = new UnauthorizedObjectResult(new
            {
                error = "unauthorized",
                detail = "Invalid, expired, or insufficient scope for the provided API key."
            });
        }
    }
}
