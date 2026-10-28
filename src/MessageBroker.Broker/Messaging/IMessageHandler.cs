namespace MessageBrokerDemo.Broker.Messaging;

public interface IMessageHandler
{
    Task HandleAsync(string routingKey, string payload, CancellationToken cancellationToken);
}
