using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Nop.Core.Domain.Catalog;
using Nop.Plugin.Widgets.SeoEnhancements.Domain;
using Nop.Services.Logging;

namespace Nop.Plugin.Widgets.SeoEnhancements.Services;

public class FaqGenerationService : IFaqGenerationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SeoEnhancementsSettings _settings;
    private readonly ILogger _logger;

    private const string ClientName = "SeoEnhancements.AzureOpenAi";

    public FaqGenerationService(
        IHttpClientFactory httpClientFactory,
        SeoEnhancementsSettings settings,
        ILogger logger)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings;
        _logger = logger;
    }

    public async Task<IList<GeneratedFaqPair>> GenerateForProductAsync(Product product, int pairCount)
    {
        var context = $"Product name: {product.Name}\n" +
                       $"Short description: {StripHtml(product.ShortDescription)}\n" +
                       $"Full description: {StripHtml(product.FullDescription)}";

        return await GenerateAsync(context, "product", pairCount);
    }

    public async Task<IList<GeneratedFaqPair>> GenerateForCategoryAsync(Category category, int pairCount)
    {
        var context = $"Category name: {category.Name}\n" +
                       $"Description: {StripHtml(category.Description)}";

        return await GenerateAsync(context, "category", pairCount);
    }

    // -------------------------------------------------------------------------
    // Core call
    // -------------------------------------------------------------------------

    private async Task<IList<GeneratedFaqPair>> GenerateAsync(string context, string entityKind, int pairCount)
    {
        if (string.IsNullOrWhiteSpace(_settings.AzureOpenAiEndpoint) ||
            string.IsNullOrWhiteSpace(_settings.AzureOpenAiApiKey) ||
            string.IsNullOrWhiteSpace(_settings.AzureOpenAiDeploymentName))
        {
            await _logger.WarningAsync("SeoEnhancements: Azure OpenAI settings are not configured. Skipping FAQ generation.");
            return new List<GeneratedFaqPair>();
        }

        try
        {
            var client = _httpClientFactory.CreateClient(ClientName);

            var endpoint = _settings.AzureOpenAiEndpoint.TrimEnd('/');
            var url = $"{endpoint}/openai/deployments/{_settings.AzureOpenAiDeploymentName}/chat/completions?api-version={_settings.AzureOpenAiApiVersion}";

            var systemPrompt =
                "You are an ecommerce copywriter generating FAQ content for a UK farm/cottage retail website. " +
                "Write natural, specific questions a real customer would search for, and concise, accurate answers " +
                "based only on the information given. Do not invent facts (e.g. materials, certifications, origin) " +
                "that are not in the supplied description. Keep answers to 1-3 sentences. " +
                "Respond ONLY with valid JSON — no markdown, no preamble — in this exact shape: " +
                "{\"faqs\":[{\"question\":\"...\",\"answer\":\"...\"}]}";

            var userPrompt =
                $"Generate exactly {pairCount} FAQ question-and-answer pairs for this {entityKind}.\n\n{context}";

            var requestBody = new
            {
                messages = new object[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                temperature = 0.5,
                max_tokens = 1200,
                response_format = new { type = "json_object" }
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("api-key", _settings.AzureOpenAiApiKey);
            request.Content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json");

            var response = await client.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                await _logger.ErrorAsync($"SeoEnhancements: Azure OpenAI call failed ({response.StatusCode}): {errorBody}");
                return new List<GeneratedFaqPair>();
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            return ParseFaqResponse(responseJson);
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync("SeoEnhancements: Exception generating FAQs via Azure OpenAI", ex);
            return new List<GeneratedFaqPair>();
        }
    }

    // -------------------------------------------------------------------------
    // Parsing
    // -------------------------------------------------------------------------

    private List<GeneratedFaqPair> ParseFaqResponse(string responseJson)
    {
        var result = new List<GeneratedFaqPair>();

        using var doc = JsonDocument.Parse(responseJson);

        // Standard chat completions shape: choices[0].message.content (a JSON string)
        var contentText = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        if (string.IsNullOrWhiteSpace(contentText))
            return result;

        using var inner = JsonDocument.Parse(contentText);
        if (!inner.RootElement.TryGetProperty("faqs", out var faqsArray))
            return result;

        foreach (var item in faqsArray.EnumerateArray())
        {
            var question = item.TryGetProperty("question", out var q) ? q.GetString() : null;
            var answer = item.TryGetProperty("answer", out var a) ? a.GetString() : null;

            if (!string.IsNullOrWhiteSpace(question) && !string.IsNullOrWhiteSpace(answer))
            {
                result.Add(new GeneratedFaqPair { Question = question.Trim(), Answer = answer.Trim() });
            }
        }

        return result;
    }

    private static string StripHtml(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        return System.Text.RegularExpressions.Regex.Replace(input, "<.*?>", string.Empty).Trim();
    }
}
