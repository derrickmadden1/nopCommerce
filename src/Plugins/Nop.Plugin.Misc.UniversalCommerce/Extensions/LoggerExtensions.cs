using System;
using System.Text.Json;
using System.Threading.Tasks;
using Nop.Core.Domain.Customers;
using Nop.Services.Logging;

namespace Nop.Plugin.Misc.UniversalCommerce.Extensions
{
    public static class LoggerExtensions
    {
        /// <summary>
        /// Logs a structured Agentic Commerce transaction event safely to the core database tables
        /// </summary>
        public static async Task LogAgentActivityAsync(
            this ILogger logger,
            string activityType,
            string sku,
            string message,
            Customer? customer = null)
        {
            // Build a structured payload to ensure easy log parsing later
            var structuredPayload = new
            {
                Timestamp = DateTime.UtcNow,
                Source = "GoogleAgenticCommerce",
                Activity = activityType,
                TargetSku = sku,
                Details = message
            };

            string fullMessage = JsonSerializer.Serialize(structuredPayload);
            string shortMessage = $"UCP Agent Event: {activityType} for SKU [{sku}]";

            // Insert directly into the native nopCommerce DB Log tables
            await logger.InformationAsync(
                message: shortMessage + " - " + fullMessage,
                customer: customer
            );
        }
    }
}
