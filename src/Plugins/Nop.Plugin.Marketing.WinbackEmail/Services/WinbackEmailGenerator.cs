using System.Text.Json;
using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Logging;
using Nop.Plugin.Marketing.WinbackEmail.Models;

namespace Nop.Plugin.Marketing.WinbackEmail.Services;

public class WinbackEmailGenerator
{
    private readonly WinbackEmailSettings _settings;
    private readonly Nop.Services.Logging.ILogger _logger;

    public WinbackEmailGenerator(
        WinbackEmailSettings settings,
        Nop.Services.Logging.ILogger logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public async Task<GeneratedEmail?> GenerateAsync(WinbackCustomerContext context)
    {
        try
        {
            var apiKey = _settings.AzureOpenAIApiKey;
            
            if (_settings.UseAzureKeyVault)
            {
                if (string.IsNullOrWhiteSpace(_settings.AzureKeyVaultUrl) || string.IsNullOrWhiteSpace(_settings.AzureKeyVaultSecretName))
                {
                    await _logger.ErrorAsync("WinbackEmail: Azure Key Vault is enabled but URL or Secret Name is missing.");
                    return null;
                }

                var secretClient = new SecretClient(new Uri(_settings.AzureKeyVaultUrl), new DefaultAzureCredential());
                var secretResponse = await secretClient.GetSecretAsync(_settings.AzureKeyVaultSecretName);
                apiKey = secretResponse.Value.Value;
            }

            var client = new OpenAIClient(
                new Uri(_settings.AzureOpenAIEndpoint),
                new AzureKeyCredential(apiKey)
            );

            var systemPrompt = BuildSystemPrompt();
            var userPrompt = BuildUserPrompt(context);

            var options = new ChatCompletionsOptions
            {
                DeploymentName = _settings.DeploymentName,
                Messages =
                {
                    new ChatRequestSystemMessage(systemPrompt),
                    new ChatRequestUserMessage(userPrompt)
                }
            };

            var response = await client.GetChatCompletionsAsync(options);
            var content = response.Value.Choices[0].Message.Content;

            return await ParseResponseAsync(content);
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync($"Failed to generate winback email for {context.CustomerEmail}", ex);
            return null;
        }
    }

    private static string BuildSystemPrompt() => """
        You are an email copywriter for a small UK-based e-commerce store.
        Write warm, personal, and genuine winback emails — never pushy or salesy.
        Always respond with valid JSON in this exact format:
        {
          "subject": "email subject line here",
          "htmlBody": "full html email body here"
        }
        The HTML body should be clean, simple, mobile-friendly HTML.
        Use British English spelling throughout.
        Do not include any text outside the JSON object.
        """;

    private static string BuildUserPrompt(WinbackCustomerContext context)
    {
        var orderHistory = context.RecentOrders.Any()
            ? string.Join("\n", context.RecentOrders.Select(o =>
                $"- {o.OrderDate:d MMM yyyy}: {string.Join(", ", o.ProductNames)} (£{o.OrderTotal:F2})"))
            : "No order history available";

        var emailAngle = context.EmailNumber switch
        {
            1 => "A warm, genuine 'we miss you' message. No discount. Just a personal, friendly check-in that reminds them of their positive experience with the store.",
            2 => "Highlight what's new or popular in the store since their last visit. Make it feel like an insider update, not a promotional blast.",
            3 => $"A final gentle nudge. Include the discount code '{context.DiscountCode}' if provided (only mention it if it's not empty). Keep it light — no pressure.",
            _ => "A warm re-engagement message."
        };

        return $"""
            Write winback email #{context.EmailNumber} of 3 for this customer:

            Customer name: {context.CustomerFirstName}
            Days since last order: {context.DaysSinceLastOrder}
            Store name: {context.StoreName}
            Discount code (email 3 only, may be empty): {context.DiscountCode ?? "none"}

            Their order history:
            {orderHistory}

            Email angle: {emailAngle}

            Keep the subject line under 50 characters.
            Keep the body concise — 3 to 5 short paragraphs maximum.
            Sign off warmly from the {context.StoreName} team.
            """;
    }

    private class EmailResponse
    {
        public string? Subject { get; set; }
        public string? HtmlBody { get; set; }
    }

    private async Task<GeneratedEmail?> ParseResponseAsync(string content)
    {
        try
        {
            var cleaned = content
                .Replace("```json", "")
                .Replace("```", "")
                .Trim();

            var result = JsonSerializer.Deserialize<EmailResponse>(cleaned, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (result == null) return null;

            return new GeneratedEmail
            {
                Subject = result.Subject ?? string.Empty,
                HtmlBody = result.HtmlBody ?? string.Empty
            };
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync($"WinbackEmail: Failed to parse JSON. Raw content: {content}", ex);
            return null;
        }
    }
}
