using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Nop.Core.Events;
using Nop.Plugin.Widgets.MarketLocator;
using Nop.Plugin.Widgets.MarketLocator.Domain;
using Nop.Plugin.Widgets.MarketLocator.Messaging.Messages;
using Nop.Plugin.Widgets.MarketLocator.Services;
using Nop.Services.Events;
using Nop.Services.Messaging;

public class MarketLocationEventConsumer :
    IConsumer<EntityInsertedEvent<MarketLocation>>,
    IConsumer<EntityUpdatedEvent<MarketLocation>>,
    IConsumer<EntityDeletedEvent<MarketLocation>>
{
    private const string QueueName = "market-social-posts";

    private readonly IServiceBusPublisher _publisher;
    private readonly IMarketLocationService _marketLocationService;
    private readonly ILogger<MarketLocationEventConsumer> _logger;
    private readonly MarketLocatorSettings _settings;

    private int DaysBeforeMarket => _settings.SocialPublishDaysBeforeMarket;

    public MarketLocationEventConsumer(
        IServiceBusPublisher publisher,
        IMarketLocationService marketLocationService,
        ILogger<MarketLocationEventConsumer> logger,
        MarketLocatorSettings settings)
    {
        _publisher = publisher;
        _marketLocationService = marketLocationService;
        _logger = logger;
        _settings = settings;
    }

    public async Task HandleEventAsync(EntityInsertedEvent<MarketLocation> eventMessage)
        => await HandleCreatedAsync(eventMessage.Entity);

    public async Task HandleEventAsync(EntityUpdatedEvent<MarketLocation> eventMessage)
        => await HandleUpdatedAsync(eventMessage.Entity);

    public async Task HandleEventAsync(EntityDeletedEvent<MarketLocation> eventMessage)
        => await HandleDeletedAsync(eventMessage.Entity);

    // --- Created ---

    private async Task HandleCreatedAsync(MarketLocation market)
    {
        if (!ShouldPublish(market))
            return;

        try
        {
            var (message, scheduledTime) = BuildMessageAndTime(market, "Created");
            long? sequenceNumber = null;

            if (scheduledTime.HasValue)
            {
                sequenceNumber = await _publisher.ScheduleAsync(QueueName, message, scheduledTime.Value);
            }
            else
            {
                await _publisher.PublishAsync(QueueName, message);
            }

            // Persist the sequence number so we can cancel later if needed
            market.PendingSocialPostSequenceNumber = sequenceNumber;
            await _marketLocationService.UpdateAsync(market);
        }
        catch (ServiceBusException ex) when (ex.IsTransient)
        {
            _logger.LogWarning(ex, "Transient Service Bus error on Created for {MarketName}", market.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling Created for {MarketName}", market.Name);
        }
    }

    // --- Updated ---

    [ThreadStatic]
    private static bool _processingUpdate;
    private async Task HandleUpdatedAsync(MarketLocation market)
    {
    // Guard against re-entrant updates triggered by persisting the sequence number
    if (_processingUpdate)
            return;

        _processingUpdate = true;

        try
        {
            if (!ShouldPublish(market))
            {
                // Market has been unpublished — cancel any pending post
                await TryCancelPendingAsync(market);
                return;
            }

            try
            {
                // Cancel the existing scheduled message if one exists
                await TryCancelPendingAsync(market);

                // Re-schedule with the (potentially new) date
                var (message, scheduledTime) = BuildMessageAndTime(market, "Updated");
                long? sequenceNumber = null;

                if (scheduledTime.HasValue)
                {
                    sequenceNumber = await _publisher.ScheduleAsync(QueueName, message, scheduledTime.Value);
                    _logger.LogInformation(
                        "Rescheduled social post for {MarketName} — new sequence {SequenceNumber}",
                        market.Name, sequenceNumber);
                }
                else
                {
                    // Market is imminent — post immediately
                    await _publisher.PublishAsync(QueueName, message);
                }

                market.PendingSocialPostSequenceNumber = sequenceNumber;
                await _marketLocationService.UpdateAsync(market);
            }
            catch (ServiceBusException ex) when (ex.IsTransient)
            {
                _logger.LogWarning(ex, "Transient Service Bus error on Updated for {MarketName}", market.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling Updated for {MarketName}", market.Name);
            }
        }
        finally
        {
            _processingUpdate = false;
        }
    }

    // --- Deleted ---

    private async Task HandleDeletedAsync(MarketLocation market)
    {
        await TryCancelPendingAsync(market);
    }

    // --- Helpers ---

    private async Task TryCancelPendingAsync(MarketLocation market)
    {
        if (!market.PendingSocialPostSequenceNumber.HasValue)
            return;

        try
        {
            await _publisher.CancelScheduledAsync(
                QueueName, market.PendingSocialPostSequenceNumber.Value);

            market.PendingSocialPostSequenceNumber = null;
            await _marketLocationService.UpdateAsync(market);
        }
        catch (ServiceBusException ex) when (ex.IsTransient)
        {
            _logger.LogWarning(ex,
                "Transient error cancelling sequence {SequenceNumber} for {MarketName}",
                market.PendingSocialPostSequenceNumber, market.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error cancelling sequence {SequenceNumber} for {MarketName}",
                market.PendingSocialPostSequenceNumber, market.Name);
        }
    }

    private bool ShouldPublish(MarketLocation market)
    {
        if (!_settings.EnableSocialPublishing)
            return false;
        if (!market.Published)
            return false;
        return true;
    }

    private (MarketEventMessage message, DateTimeOffset? scheduledTime) BuildMessageAndTime(
        MarketLocation market, string changeType)
    {
        var (startDate, endDate) = MarketDateHelper.GetNextMarketOccurrence(market.UpcomingDates, market.Hours);

        var message = new MarketEventMessage
        {
            ChangeType = changeType,
            MarketName = market.Name,
            Location = market.Address,
            StartDate = startDate,
            EndDate = endDate,
            Description = market.Description,
            MapUrl = $"{_settings.StoreUrl.TrimEnd('/')}/markets?id={market.Id}"
        };

        var scheduledTime = CalculateScheduledTime(startDate);
        return (message, scheduledTime);
    }

    private DateTimeOffset? CalculateScheduledTime(DateTime? marketStartDate)
    {
        if (!marketStartDate.HasValue)
            return null;

        var postDate = marketStartDate.Value.Date.AddDays(-DaysBeforeMarket);

        // If post date is in the past or imminent, post immediately
        if (postDate <= DateTime.UtcNow.Date)
            return null;

        // Post at 9am UTC on the scheduled day
        return new DateTimeOffset(
            postDate.Year, postDate.Month, postDate.Day,
            9, 0, 0, TimeSpan.Zero);
    }
}