using System.Threading.Tasks;

namespace Nop.Services.Messaging
{
    /// <summary>
    /// Simple abstraction for publishing messages to a service bus (queues/topics).
    /// </summary>
    public interface IServiceBusPublisher
    {
        Task PublishAsync<T>(
            string topicOrQueue,
            T message,
            CancellationToken cancellationToken = default);

        Task<long> ScheduleAsync<T>(
            string topicOrQueue,
            T message,
            DateTimeOffset scheduledEnqueueTime,
            CancellationToken cancellationToken = default);

        Task CancelScheduledAsync(
            string topicOrQueue,
            long sequenceNumber,
            CancellationToken cancellationToken = default);
    }
}
