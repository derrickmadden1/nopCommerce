using System.Collections.Concurrent;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;

namespace Nop.Services.Messaging;

public class ServiceBusPublisher : IServiceBusPublisher, IAsyncDisposable
{
    private readonly ServiceBusClient _client;
    private readonly ILogger<ServiceBusPublisher> _logger;

    // Cache senders per topic/queue to avoid recreating them
    private readonly ConcurrentDictionary<string, ServiceBusSender> _senders = new();

    public ServiceBusPublisher(ServiceBusClient client, ILogger<ServiceBusPublisher> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task PublishAsync<T>(string topicOrQueue, T message, CancellationToken cancellationToken = default)
    {
        var sender = _senders.GetOrAdd(topicOrQueue, _client.CreateSender);

        var payload = JsonSerializer.Serialize(message, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var sbMessage = new ServiceBusMessage(payload)
        {
            ContentType = "application/json",
            Subject = typeof(T).Name,                          // e.g. "MarketEventMessage"
            MessageId = Guid.NewGuid().ToString(),
            ApplicationProperties =
            {
                ["MessageType"] = typeof(T).Name,
                ["PublishedAt"] = DateTimeOffset.UtcNow.ToString("O")
            }
        };

        try
        {
            await sender.SendMessageAsync(sbMessage, cancellationToken);
            _logger.LogInformation("Published {MessageType} to {Destination}", typeof(T).Name, topicOrQueue);
        }
        catch (ServiceBusException ex)
        {
            _logger.LogError(ex, "Failed to publish {MessageType} to {Destination}", typeof(T).Name, topicOrQueue);
            throw;
        }
    }

    public async Task<long> ScheduleAsync<T>(
    string topicOrQueue,
    T message,
    DateTimeOffset scheduledEnqueueTime,
    CancellationToken cancellationToken = default)
    {
        var sender = _senders.GetOrAdd(topicOrQueue, _client.CreateSender);

        var payload = JsonSerializer.Serialize(message, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var sbMessage = new ServiceBusMessage(payload)
        {
            ContentType = "application/json",
            Subject = typeof(T).Name,
            MessageId = Guid.NewGuid().ToString(),
            ApplicationProperties =
        {
            ["MessageType"] = typeof(T).Name,
            ["PublishedAt"] = DateTimeOffset.UtcNow.ToString("O")
        }
        };

        try
        {
            var sequenceNumber = await sender.ScheduleMessageAsync(
                sbMessage, scheduledEnqueueTime, cancellationToken);

            _logger.LogInformation(
                "Scheduled {MessageType} on {Destination} at {ScheduledTime} — sequence {SequenceNumber}",
                typeof(T).Name, topicOrQueue, scheduledEnqueueTime, sequenceNumber);

            return sequenceNumber;
        }
        catch (ServiceBusException ex)
        {
            _logger.LogError(ex, "Failed to schedule {MessageType} on {Destination}",
                typeof(T).Name, topicOrQueue);
            throw;
        }
    }

    public async Task CancelScheduledAsync(
        string topicOrQueue,
        long sequenceNumber,
        CancellationToken cancellationToken = default)
    {
        var sender = _senders.GetOrAdd(topicOrQueue, _client.CreateSender);

        try
        {
            await sender.CancelScheduledMessageAsync(sequenceNumber, cancellationToken);

            _logger.LogInformation(
                "Cancelled scheduled message {SequenceNumber} on {Destination}",
                sequenceNumber, topicOrQueue);
        }
        catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessageNotFound)
        {
            // Message already delivered or never existed — safe to ignore
            _logger.LogWarning(
                "Sequence {SequenceNumber} not found on {Destination} — may have already been delivered",
                sequenceNumber, topicOrQueue);
        }
        catch (ServiceBusException ex)
        {
            _logger.LogError(ex, "Failed to cancel sequence {SequenceNumber} on {Destination}",
                sequenceNumber, topicOrQueue);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var sender in _senders.Values)
            await sender.DisposeAsync();

        await _client.DisposeAsync();
    }
}