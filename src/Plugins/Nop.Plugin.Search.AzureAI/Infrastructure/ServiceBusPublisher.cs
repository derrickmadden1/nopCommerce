using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Nop.Plugin.Search.AzureAI.Messages;

namespace Nop.Plugin.Search.AzureAI.Infrastructure;

public class ServiceBusPublisher : IAsyncDisposable
{
    private readonly AzureAISearchSettings _settings;
    private readonly ILogger<ServiceBusPublisher> _logger;
    private ServiceBusClient? _client;
    private ServiceBusSender? _sender;
    private readonly HashSet<string> _publishedMessages = new();

    public ServiceBusPublisher(
        AzureAISearchSettings settings,
        ILogger<ServiceBusPublisher> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public async Task PublishAsync(ProductIndexMessage message)
    {
        var key = $"{message.Action}:{message.ProductId}";
        if (!_publishedMessages.Add(key))
        {
            _logger.LogInformation("Skipping duplicate {Action} message for product {ProductId} in the same request scope.", message.Action, message.ProductId);
            return;
        }

        if (string.IsNullOrWhiteSpace(_settings.ServiceBusConnectionString) ||
            string.IsNullOrWhiteSpace(_settings.ServiceBusQueueName))
        {
            _logger.LogWarning("Service Bus is not configured — skipping product index message for product {ProductId}", message.ProductId);
            return;
        }

        try
        {
            var sender = GetSender();
            var json = JsonSerializer.Serialize(message);
            var busMessage = new ServiceBusMessage(json)
            {
                ContentType = "application/json",
                MessageId = $"{message.Action}-{message.ProductId}-{message.OccurredAtUtc.Ticks}"
            };

            await sender.SendMessageAsync(busMessage);
            _logger.LogInformation("Published {Action} message for product {ProductId}", message.Action, message.ProductId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish Service Bus message for product {ProductId}", message.ProductId);
        }
    }

    public void ResetClient()
    {
        _sender?.DisposeAsync();
        _client?.DisposeAsync();
        _sender = null;
        _client = null;
    }

    private ServiceBusSender GetSender()
    {
        if (_client == null || _sender == null)
        {
            _client = new ServiceBusClient(_settings.ServiceBusConnectionString);
            _sender = _client.CreateSender(_settings.ServiceBusQueueName);
        }

        return _sender;
    }

    public async ValueTask DisposeAsync()
    {
        if (_sender != null)
            await _sender.DisposeAsync();
        if (_client != null)
            await _client.DisposeAsync();
    }
}
