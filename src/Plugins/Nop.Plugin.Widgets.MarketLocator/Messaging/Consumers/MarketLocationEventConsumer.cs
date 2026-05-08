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
    private readonly IServiceBusPublisher _publisher;
    private readonly IMarketLocationService _marketLocationService;
    private readonly ILogger<MarketLocationEventConsumer> _logger;
    private readonly MarketLocatorSettings _settings;
    private readonly Nop.Core.Configuration.AppSettings _appSettings;

    private string QueueName
    {
        get
        {
            var config = _appSettings.Get<MarketLocatorConfig>();
            return config == null || string.IsNullOrEmpty(config.QueueName) ? "market-social-posts" : config.QueueName;
        }
    }

    private int DaysBeforeMarket => _settings.SocialPublishDaysBeforeMarket;

    public MarketLocationEventConsumer(
        IServiceBusPublisher publisher,
        IMarketLocationService marketLocationService,
        ILogger<MarketLocationEventConsumer> logger,
        MarketLocatorSettings settings,
        Nop.Core.Configuration.AppSettings appSettings)
    {
        _publisher = publisher;
        _marketLocationService = marketLocationService;
        _logger = logger;
        _settings = settings;
        _appSettings = appSettings;
    }

    public async Task HandleEventAsync(EntityInsertedEvent<MarketLocation> eventMessage)
        => await HandleCreatedAsync(eventMessage.Entity);

    public async Task HandleEventAsync(EntityUpdatedEvent<MarketLocation> eventMessage)
        => await HandleUpdatedAsync(eventMessage.Entity);

    public async Task HandleEventAsync(EntityDeletedEvent<MarketLocation> eventMessage)
        => await HandleDeletedAsync(eventMessage.Entity);

    // --- Created ---

    private static readonly AsyncLocal<bool> _processingUpdate = new AsyncLocal<bool>();

    private async Task HandleCreatedAsync(MarketLocation market)
    {
        if (_processingUpdate.Value)
            return;

        _processingUpdate.Value = true;
        try
        {
            if (!ShouldPublish(market))
                return;

            try
            {
                var schedules = BuildMessagesAndTimes(market, "Created");
                var sequenceNumbers = new List<long>();

                foreach (var (message, scheduledTime) in schedules)
                {
                    if (scheduledTime.HasValue)
                    {
                        var seq = await _publisher.ScheduleAsync(QueueName, message, scheduledTime.Value);
                        sequenceNumbers.Add(seq);
                    }
                    else
                    {
                        await _publisher.PublishAsync(QueueName, message);
                    }
                }

                // Persist the sequence numbers so we can cancel later if needed
                market.PendingSocialPostSequenceNumbers = string.Join(",", sequenceNumbers);
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
        finally
        {
            _processingUpdate.Value = false;
        }
    }

    // --- Updated ---

    private async Task HandleUpdatedAsync(MarketLocation market)
    {
        // Guard against re-entrant updates triggered by persisting the sequence number
        if (_processingUpdate.Value)
            return;

        _processingUpdate.Value = true;

        try
        {
            if (!ShouldPublish(market))
            {
                // Market has been unpublished — cancel any pending post
                await TryCancelPendingAsync(market, saveToDb: true);
                return;
            }

            try
            {
                // Cancel the existing scheduled messages if they exist (don't save DB yet)
                await TryCancelPendingAsync(market, saveToDb: false);

                // Re-schedule with the (potentially new) dates
                var schedules = BuildMessagesAndTimes(market, "Updated");
                var sequenceNumbers = new List<long>();

                foreach (var (message, scheduledTime) in schedules)
                {
                    if (scheduledTime.HasValue)
                    {
                        var seq = await _publisher.ScheduleAsync(QueueName, message, scheduledTime.Value);
                        sequenceNumbers.Add(seq);
                        _logger.LogInformation(
                            "Rescheduled social post for {MarketName} — new sequence {SequenceNumber}",
                            market.Name, seq);
                    }
                    else
                    {
                        // Market is imminent — post immediately
                        await _publisher.PublishAsync(QueueName, message);
                    }
                }

                market.PendingSocialPostSequenceNumbers = string.Join(",", sequenceNumbers);
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
            _processingUpdate.Value = false;
        }
    }

    // --- Deleted ---

    private async Task HandleDeletedAsync(MarketLocation market)
    {
        // Do not attempt to update the entity in DB as it is already deleted
        await TryCancelPendingAsync(market, saveToDb: false);
    }

    // --- Helpers ---

    private async Task TryCancelPendingAsync(MarketLocation market, bool saveToDb)
    {
        if (string.IsNullOrEmpty(market.PendingSocialPostSequenceNumbers))
            return;

        try
        {
            var sequences = market.PendingSocialPostSequenceNumbers
                .Split(',')
                .Where(s => long.TryParse(s.Trim(), out _))
                .Select(s => long.Parse(s.Trim()))
                .ToList();

            foreach (var seq in sequences)
            {
                try
                {
                    await _publisher.CancelScheduledAsync(QueueName, seq);
                }
                catch (ServiceBusException ex) when (ex.IsTransient)
                {
                    _logger.LogWarning(ex, "Transient error cancelling sequence {SequenceNumber} for {MarketName}", seq, market.Name);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error cancelling sequence {SequenceNumber} for {MarketName}", seq, market.Name);
                }
            }

            market.PendingSocialPostSequenceNumbers = string.Empty;
            if (saveToDb)
            {
                await _marketLocationService.UpdateAsync(market);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing cancelation for {MarketName}", market.Name);
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

    private List<(MarketEventMessage message, DateTimeOffset? scheduledTime)> BuildMessagesAndTimes(
        MarketLocation market, string changeType)
    {
        var occurrences = MarketDateHelper.GetAllFutureMarketOccurrences(market.UpcomingDates, market.Hours);
        var results = new List<(MarketEventMessage, DateTimeOffset?)>();

        foreach (var (startDate, endDate) in occurrences)
        {
            var message = new MarketEventMessage
            {
                ChangeType = changeType,
                MarketName = market.Name,
                Location = market.Address,
                StartDate = startDate,
                EndDate = endDate,
                Description = market.Description,
                MapUrl = $"{_settings.StoreUrl.TrimEnd('/')}/market-locations?id={market.Id}"
            };

            var scheduledTime = CalculateScheduledTime(startDate);
            results.Add((message, scheduledTime));
        }

        return results;
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