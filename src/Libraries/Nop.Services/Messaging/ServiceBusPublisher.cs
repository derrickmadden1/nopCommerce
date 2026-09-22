using System.Collections.Concurrent;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;

namespace Nop.Services.Messaging;

public class ServiceBusPublisher : IServiceBusPublisher, IAsyncDisposable
{
    private readonly ServiceBusClient? _defaultClient;
    private readonly ILogger<ServiceBusPublisher> _logger;
    private readonly IDictionary<string, string>? _connectionStringMap;
    private readonly ConcurrentDictionary<string, ServiceBusClient> _clients = new();
    private readonly ConcurrentDictionary<string, ServiceBusSender> _senders = new();

    public ServiceBusPublisher(ServiceBusClient client, ILogger<ServiceBusPublisher> logger)
    {
        _defaultClient = client;
        _logger = logger;
    }

    public ServiceBusPublisher(ILogger<ServiceBusPublisher> logger, IDictionary<string, string> connectionStringMap)
    {
        _logger = logger;
        _connectionStringMap = connectionStringMap;
    }

    private ServiceBusSender GetSender(string topicOrQueue)
    {
        return _senders.GetOrAdd(topicOrQueue, queue =>
        {
            if (_connectionStringMap != null && _connectionStringMap.TryGetValue(queue, out var connStr) && !string.IsNullOrEmpty(connStr))
            {
                var client = _clients.GetOrAdd(queue, q => new ServiceBusClient(connStr, new ServiceBusClientOptions
                {
                    TransportType = ServiceBusTransportType.AmqpWebSockets
                }));
                return client.CreateSender(queue);
            }

            if (_defaultClient != null)
            {
                return _defaultClient.CreateSender(queue);
            }

            throw new InvalidOperationException($"No ServiceBusClient or connection string found for destination queue '{queue}'.");
        });
    }

    public async Task PublishAsync<T>(string topicOrQueue, T message, CancellationToken cancellationToken = default)
    {
        var sender = GetSender(topicOrQueue);

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
        var sender = GetSender(topicOrQueue);

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
        var sender = GetSender(topicOrQueue);

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

        foreach (var client in _clients.Values)
            await client.DisposeAsync();

        if (_defaultClient != null)
            await _defaultClient.DisposeAsync();
    }
}