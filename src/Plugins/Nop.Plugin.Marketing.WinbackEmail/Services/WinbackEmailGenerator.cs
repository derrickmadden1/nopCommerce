using System.Text.Json;
using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Logging;
using Nop.Plugin.Marketing.WinbackEmail.Models;

namespace Nop.Plugin.Marketing.WinbackEmail.Services;

public class WinbackEmailGenerator
{
    private readonly WinbackEmailSettings _settings;
    private readonly ILogger<WinbackEmailGenerator> _logger;

    public WinbackEmailGenerator(
        WinbackEmailSettings settings,
        ILogger<WinbackEmailGenerator> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public async Task<GeneratedEmail?> GenerateAsync(WinbackCustomerContext context)
    {
        try
        {
            var client = new OpenAIClient(
                new Uri(_settings.AzureOpenAIEndpoint),
                new AzureKeyCredential(_settings.AzureOpenAIApiKey)
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
                },
                MaxTokens = 800,
                Temperature = 0.7f
            };

            var response = await client.GetChatCompletionsAsync(options);
            var content = response.Value.Choices[0].Message.Content;

            return ParseResponse(content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate winback email for {Email}", context.CustomerEmail);
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

    private static GeneratedEmail? ParseResponse(string content)
    {
        try
        {
            // Strip markdown code fences if present
            var cleaned = content
                .Replace("```json", "")
                .Replace("```", "")
                .Trim();

            var result = JsonSerializer.Deserialize<JsonElement>(cleaned);

            return new GeneratedEmail
            {
                Subject = result.GetProperty("subject").GetString() ?? string.Empty,
                HtmlBody = result.GetProperty("htmlBody").GetString() ?? string.Empty
            };
        }
        catch
        {
            return null;
        }
    }
}
