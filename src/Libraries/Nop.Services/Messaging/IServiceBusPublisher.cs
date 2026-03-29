using System.Threading.Tasks;

namespace Nop.Services.Messaging
{
    /// <summary>
    /// Simple abstraction for publishing messages to a service bus (queues/topics).
    /// </summary>
    public interface IServiceBusPublisher
    {
        /// <summary>
        /// Publish a message to the specified destination (queue or topic).
        /// </summary>
        Task PublishAsync(string destination, object message);
    }
}
