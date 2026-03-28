using Nop.Core.Events;
using Nop.Plugin.Widgets.MarketLocator.Domain;
using Nop.Services.Events;

public class MarketLocationEventConsumer :
    IConsumer<EntityInsertedEvent<MarketLocation>>,
    IConsumer<EntityUpdatedEvent<MarketLocation>>
{
    private readonly IServiceBusPublisher _publisher;

    public MarketLocationEventConsumer(IServiceBusPublisher publisher)
    {
        _publisher = publisher;
    }

    public async Task HandleEventAsync(EntityInsertedEvent<MarketLocation> eventMessage)
        => await PublishAsync(eventMessage.Entity, "Created");

    public async Task HandleEventAsync(EntityUpdatedEvent<MarketLocation> eventMessage)
        => await PublishAsync(eventMessage.Entity, "Updated");

    private async Task PublishAsync(MarketLocation market, string changeType)
    {
        var message = new MarketEventMessage
        {
            ChangeType = changeType,
            MarketName = market.Name,
            Location = market.Address,
            StartDate = market.StartDate,
            EndDate = market.EndDate,
            Description = market.Description,
            MapUrl = $"https://yoursite.com/markets?id={market.Id}"
        };

        await _publisher.PublishAsync("market-events", message);
    }
}