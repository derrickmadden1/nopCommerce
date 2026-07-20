using System.Text.Json;
using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Logging;
using Nop.Plugin.Widgets.AiChatbot.Models;

namespace Nop.Plugin.Widgets.AiChatbot.Services;

public class ChatService
{
    private readonly AiChatbotSettings _settings;
    private readonly CustomerContextService _customerContextService;
    private readonly ProductSearchService _productSearchService;
    private readonly ILogger<ChatService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ChatService(
        AiChatbotSettings settings,
        CustomerContextService customerContextService,
        ProductSearchService productSearchService,
        ILogger<ChatService> logger)
    {
        _settings = settings;
        _customerContextService = customerContextService;
        _productSearchService = productSearchService;
        _logger = logger;
    }

    public async Task<ChatResponse> GetResponseAsync(ChatRequest request)
    {
        // Programmatic Guardrail: Pre-filter query for prompt injections and jailbreak keywords
        var jailbreakKeywords = new[] { "ignore previous", "system prompt", "developer mode", "jailbreak", "dan mode" };
        if (jailbreakKeywords.Any(k => request.Message.Contains(k, StringComparison.OrdinalIgnoreCase)))
        {
            return new ChatResponse
            {
                Response = "I can only assist you with store-related enquiries, orders, and products.",
                Success = true
            };
        }

        try
        {
            var apiKey = _settings.AzureOpenAIApiKey;
            if (_settings.UseAzureKeyVault)
            {
                var secretClient = new SecretClient(new Uri(_settings.AzureKeyVaultUrl), new DefaultAzureCredential());
                var secretResponse = await secretClient.GetSecretAsync(_settings.AzureKeyVaultSecretName);
                apiKey = secretResponse.Value.Value;
            }

            var client = new OpenAIClient(
                new Uri(_settings.AzureOpenAIEndpoint),
                new AzureKeyCredential(apiKey)
            );

            // Build context in parallel
            var customerContextTask = _customerContextService.GetCurrentCustomerContextAsync();
            var relevantProductsTask = _productSearchService.SearchAsync(request.Message);

            await Task.WhenAll(customerContextTask, relevantProductsTask);

            var customerContext = customerContextTask.Result;
            var relevantProductsList = relevantProductsTask.Result;
            var relevantProducts = ProductSearchService.FormatForPrompt(relevantProductsList);

            var systemPrompt = BuildSystemPrompt(customerContext, relevantProducts);

            // Build messages — system prompt + capped history + new message
            var messages = new List<ChatRequestMessage>
            {
                new ChatRequestSystemMessage(systemPrompt)
            };

            // Cap history to avoid exceeding context window
            var cappedHistory = request.History
                .TakeLast(_settings.MaxConversationTurns * 2)
                .ToList();

            foreach (var turn in cappedHistory)
            {
                if (turn.Role == "user")
                    messages.Add(new ChatRequestUserMessage(turn.Content));
                else if (turn.Role == "assistant")
                    messages.Add(new ChatRequestAssistantMessage(turn.Content));
            }

            messages.Add(new ChatRequestUserMessage(request.Message));

            var options = new ChatCompletionsOptions
            {
                DeploymentName = _settings.DeploymentName,
                ResponseFormat = ChatCompletionsResponseFormat.JsonObject
            };

            if (_settings.MaxTokens.HasValue)
                options.MaxTokens = _settings.MaxTokens.Value;

            if (_settings.Temperature.HasValue)
                options.Temperature = _settings.Temperature.Value;

            foreach (var message in messages)
                options.Messages.Add(message);

            var response = await client.GetChatCompletionsAsync(options);
            var reply = response.Value.Choices[0].Message.Content;

            return ParseStructuredResponse(reply);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Chat completion failed for message: {Message}", request.Message);
            return new ChatResponse
            {
                Success = false,
                Error = "Sorry, I'm having trouble responding right now. Please try again in a moment."
            };
        }
    }

    private ChatResponse ParseStructuredResponse(string content)
    {
        try
        {
            // Strip markdown fences if present
            var cleaned = content
                .Replace("```json", "")
                .Replace("```", "")
                .Trim();

            var structured = JsonSerializer.Deserialize<AiStructuredResponse>(cleaned, JsonOptions);

            if (structured == null)
                return new ChatResponse { Response = content, Success = true };

            var chatResponse = new ChatResponse
            {
                Response = structured.Message,
                Success = true
            };

            // Map AI action to ChatAction
            if (structured.Action != null)
            {
                chatResponse.Action = new ChatAction
                {
                    Type = structured.Action.Type,
                    Url = structured.Action.Url,
                    ProductId = structured.Action.ProductId,
                    Quantity = structured.Action.Quantity > 0 ? structured.Action.Quantity : 1,
                    Label = structured.Action.Label
                };
            }

            return chatResponse;
        }
        catch
        {
            // If JSON parsing fails, treat the whole response as plain text
            // This handles cases where the AI doesn't return JSON
            return new ChatResponse { Response = content, Success = true };
        }
    }

    private string BuildSystemPrompt(CustomerContext customer, string relevantProducts)
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine($$"""
            You are {{_settings.BotName}}, a helpful and friendly shopping assistant for {{_settings.StoreName}}.
            You help customers with order status, product questions, and store policies.
            
            CRITICAL: You are strictly limited to store operations and informational assistance. You cannot programmatically modify past orders or process payments. If a customer asks to add an item to the basket or go to checkout, you can trigger these actions by returning the structured action JSON. Make sure to describe the action in your message response.
            Only recommend or discuss products that are listed in the 'Products relevant to their query' section below, or have already been mentioned in the conversation history. If a product is not listed and has not been mentioned, explain that you don't carry that item.
            
            Always be warm, concise, and use British English spelling.
            Never make up information — if you don't know something, say so and offer to help another way. Never promise to contact the workshop, human staff, or follow up with the customer later (as you do not have the ability to send emails or create support tickets). If you don't know the answer, advise them to use the 'Contact Us' page or contact support directly.
            Keep responses short and conversational — 2-3 sentences where possible.
            Use plain text only — no markdown, no bullet points, no bold text.
            IMPORTANT: You must ALWAYS respond with a valid JSON object in this exact format:
            {
              "message": "Your friendly response here",
              "action": null
            }

            Or if an action is needed:
            {
              "message": "Your friendly response here",
              "action": {
                "type": "addToCart",
                "productId": 42,
                "quantity": 1,
                "label": "Product Name"
              }
            }

            Available action types:
            - addToCart: { "type": "addToCart", "productId": 123, "quantity": 1, "label": "Product name" }
            - navigate: { "type": "navigate", "url": "/checkout", "label": "checkout" }
            - viewCart: { "type": "navigate", "url": "/cart", "label": "cart" }

            Navigation URLs:
            - Cart: /cart
            - Checkout: /checkout
            - Search results: /search?q=QUERY (url-encode the query)
            - Product page: /PRODUCT-SENAME (use the product's URL if known)

            Rules:
            - Only trigger addToCart if the customer explicitly asks to add something. In your message, tell them to click the button below to add it. Do not say "I have added it" or claim that the item is already in their cart.
            - Only trigger navigation if the customer explicitly asks to go somewhere. In your message, tell them to click the button below to go there.
            - For product pages use /search?q=PRODUCTNAME if you don't know the exact URL
            - Keep the message short — 1-3 sentences
            - Do not include markdown in the message field
            - Do not output any text outside the JSON object
            """);

        // Customer context
        if (customer.IsLoggedIn && customer.FirstName != null)
        {
            sb.AppendLine($"\nThe customer's name is {customer.FirstName}.");

            if (customer.RecentOrders.Any())
            {
                sb.AppendLine("\nTheir recent orders:");
                foreach (var order in customer.RecentOrders)
                {
                    sb.AppendLine($"- Order {order.OrderNumber} placed on {order.OrderDate:d MMM yyyy}: " +
                                  $"{string.Join(", ", order.Products)} — " +
                                  $"Status: {order.Status} — Total: £{order.OrderTotal:F2}" +
                                  (order.TrackingNumber != null ? $" — Tracking: {order.TrackingNumber}" : ""));
                }
            }
            else
            {
                sb.AppendLine("\nThis customer has no previous orders.");
            }
        }
        else
        {
            sb.AppendLine("\nThe customer is not logged in. You do not have access to their order history.");
            sb.AppendLine("If they ask about an order, politely ask them to log in for order details.");
        }

        // Relevant products from search
        if (!string.IsNullOrWhiteSpace(relevantProducts))
        {
            sb.AppendLine($"\nProducts relevant to their query:");
            sb.AppendLine(relevantProducts);
        }

        // Store policies
        if (!string.IsNullOrWhiteSpace(_settings.ReturnsPolicy))
        {
            sb.AppendLine($"\nReturns policy:\n{_settings.ReturnsPolicy}");
        }

        if (!string.IsNullOrWhiteSpace(_settings.ShippingPolicy))
        {
            sb.AppendLine($"\nShipping policy:\n{_settings.ShippingPolicy}");
        }

        return sb.ToString();
    }
}
