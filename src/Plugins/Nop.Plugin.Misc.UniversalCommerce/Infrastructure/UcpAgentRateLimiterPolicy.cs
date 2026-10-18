using System;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Nop.Plugin.Misc.UniversalCommerce.Domain;

namespace Nop.Plugin.Misc.UniversalCommerce.Infrastructure
{
    public class UcpAgentRateLimiterPolicy : IRateLimiterPolicy<string>
    {
        public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected => async (context, token) =>
        {
            context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.HttpContext.Response.ContentType = "application/json";
            await context.HttpContext.Response.WriteAsync(
                "{\"error\": \"Too many requests. Agent traffic threshold exceeded.\"}",
                cancellationToken: token);
        };

        public RateLimitPartition<string> GetPartition(HttpContext httpContext)
        {
            var ucpSettings = httpContext.RequestServices.GetRequiredService<UcpSettings>();
            return RateLimitPartition.GetFixedWindowLimiter("UcpAgentPolicy", partition => new FixedWindowRateLimiterOptions
            {
                PermitLimit = ucpSettings.PermitLimit > 0 ? ucpSettings.PermitLimit : 100,
                Window = TimeSpan.FromSeconds(ucpSettings.WindowInSeconds > 0 ? ucpSettings.WindowInSeconds : 60),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = ucpSettings.QueueLimit >= 0 ? ucpSettings.QueueLimit : 0
            });
        }
    }
}
