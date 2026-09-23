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
    private readonly IServiceBusPublisher? _publisher;
    private readonly IMarketLocationService _marketLocationService;
    private readonly ILogger<MarketLocationEventConsumer> _logger;
    private readonly MarketLocatorSettings _settings;
    private readonly Nop.Core.Configuration.AppSettings _appSettings;
    private readonly Nop.Services.Media.IPictureService _pictureService;

    private string QueueName
    {
        get
        {
            var config = _appSettings.Get<MarketLocatorConfig>();
            return config == null || string.IsNullOrEmpty(config.QueueName) ? "market-social-posts" : config.QueueName;
        }
    }

    private string InstagramQueueName
    {
        get
        {
            var config = _appSettings.Get<MarketLocatorConfig>();
            return config == null || string.IsNullOrEmpty(config.InstagramQueueName) ? "market-instagram-posts" : config.InstagramQueueName;
        }
    }

    private int DaysBeforeMarket => _settings.SocialPublishDaysBeforeMarket;

    public MarketLocationEventConsumer(
        IEnumerable<IServiceBusPublisher> publishers,
        IMarketLocationService marketLocationService,
        ILogger<MarketLocationEventConsumer> logger,
        MarketLocatorSettings settings,
        Nop.Core.Configuration.AppSettings appSettings,
        Nop.Services.Media.IPictureService pictureService)
    {
        _publisher = publishers.FirstOrDefault();
        _marketLocationService = marketLocationService;
        _logger = logger;
        _settings = settings;
        _appSettings = appSettings;
        _pictureService = pictureService;
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

            var targetQueues = GetTargetQueues(market);
            if (targetQueues.Count == 0)
                return;

            try
            {
                var schedules = await BuildMessagesAndTimesAsync(market, "Created");
                var sequenceEntries = new List<string>();

                foreach (var queue in targetQueues)
                {
                    foreach (var (message, scheduledTime) in schedules)
                    {
                        if (scheduledTime.HasValue)
                        {
                            var seq = await _publisher!.ScheduleAsync(queue, message, scheduledTime.Value);
                            sequenceEntries.Add($"{queue}:{seq}");
                            _logger.LogInformation(
                                "Scheduled social post for {MarketName} on queue {Queue} — sequence {SequenceNumber}",
                                market.Name, queue, seq);
                        }
                        else
                        {
                            await _publisher!.PublishAsync(queue, message);
                            _logger.LogInformation(
                                "Published immediate social post for {MarketName} on queue {Queue}",
                                market.Name, queue);
                        }
                    }
                }

                // Persist sequence entries (queue:seq) so we can cancel later if needed
                market.PendingSocialPostSequenceNumbers = string.Join(",", sequenceEntries);
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
            var targetQueues = GetTargetQueues(market);

            if (!ShouldPublish(market) || targetQueues.Count == 0)
            {
                // Market is unpublished or has no active social targets — cancel any pending post
                await TryCancelPendingAsync(market, saveToDb: true);
                return;
            }

            try
            {
                // Cancel the existing scheduled messages if they exist (don't save DB yet)
                await TryCancelPendingAsync(market, saveToDb: false);

                // Re-schedule with the (potentially new) dates/content
                var schedules = await BuildMessagesAndTimesAsync(market, "Updated");
                var sequenceEntries = new List<string>();

                foreach (var queue in targetQueues)
                {
                    foreach (var (message, scheduledTime) in schedules)
                    {
                        if (scheduledTime.HasValue)
                        {
                            var seq = await _publisher!.ScheduleAsync(queue, message, scheduledTime.Value);
                            sequenceEntries.Add($"{queue}:{seq}");
                            _logger.LogInformation(
                                "Rescheduled social post for {MarketName} on queue {Queue} — new sequence {SequenceNumber}",
                                market.Name, queue, seq);
                        }
                        else
                        {
                            // Market is imminent — post immediately
                            await _publisher!.PublishAsync(queue, message);
                            _logger.LogInformation(
                                "Published immediate social post update for {MarketName} on queue {Queue}",
                                market.Name, queue);
                        }
                    }
                }

                market.PendingSocialPostSequenceNumbers = string.Join(",", sequenceEntries);
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

    private List<string> GetTargetQueues(MarketLocation market)
    {
        var queues = new List<string>();
        if (market.PublishToFacebook && !string.IsNullOrWhiteSpace(QueueName))
        {
            queues.Add(QueueName);
        }
        if (market.PublishToInstagram && !string.IsNullOrWhiteSpace(InstagramQueueName))
        {
            queues.Add(InstagramQueueName);
        }
        return queues;
    }

    private async Task TryCancelPendingAsync(MarketLocation market, bool saveToDb)
    {
        if (_publisher == null || string.IsNullOrEmpty(market.PendingSocialPostSequenceNumbers))
            return;

        try
        {
            var rawEntries = market.PendingSocialPostSequenceNumbers.Split(',', StringSplitOptions.RemoveEmptyEntries);

            foreach (var rawEntry in rawEntries)
            {
                var entry = rawEntry.Trim();
                string targetQueue = QueueName;
                string seqStr = entry;

                if (entry.Contains(':'))
                {
                    var parts = entry.Split(':');
                    targetQueue = parts[0];
                    seqStr = parts[1];
                }

                if (long.TryParse(seqStr, out var seq))
                {
                    try
                    {
                        await _publisher.CancelScheduledAsync(targetQueue, seq);
                        _logger.LogInformation(
                            "Cancelled scheduled social post sequence {SequenceNumber} on queue {Queue} for {MarketName}",
                            seq, targetQueue, market.Name);
                    }
                    catch (ServiceBusException ex) when (ex.IsTransient)
                    {
                        _logger.LogWarning(ex, "Transient error cancelling sequence {SequenceNumber} on queue {Queue} for {MarketName}", seq, targetQueue, market.Name);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error cancelling sequence {SequenceNumber} on queue {Queue} for {MarketName}", seq, targetQueue, market.Name);
                    }
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
        if (_publisher == null)
            return false;
        if (!_settings.EnableSocialPublishing)
            return false;
        if (!market.Published)
            return false;
        return true;
    }

    private async Task<List<(MarketEventMessage message, DateTimeOffset? scheduledTime)>> BuildMessagesAndTimesAsync(
        MarketLocation market, string changeType)
    {
        var occurrences = MarketDateHelper.GetAllFutureMarketOccurrences(market.UpcomingDates, market.Hours);
        var results = new List<(MarketEventMessage, DateTimeOffset?)>();

        foreach (var (startDate, endDate) in occurrences)
        {
            var scheduledTime = CalculateScheduledTime(startDate);
            var messageChangeType = "Created";

            var message = new MarketEventMessage
            {
                ChangeType = messageChangeType,
                MarketName = market.Name,
                Location = market.Address,
                StartDate = startDate,
                EndDate = endDate,
                Description = market.Description,
                MapUrl = $"{_settings.StoreUrl.TrimEnd('/')}/market-locations?id={market.Id}"
            };

            if (market.PictureId > 0)
            {
                var pictureUrl = await _pictureService.GetPictureUrlAsync(market.PictureId, storeLocation: _settings.StoreUrl);
                if (!string.IsNullOrEmpty(pictureUrl))
                {
                    if (!pictureUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                        !pictureUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        pictureUrl = $"{_settings.StoreUrl.TrimEnd('/')}/{pictureUrl.TrimStart('/')}";
                    }
                    message.ImageUrl = pictureUrl;
                }
            }

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