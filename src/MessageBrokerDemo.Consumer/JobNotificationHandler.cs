using System.Text.Json;

using Microsoft.Extensions.Logging;

using MessageBrokerDemo.Broker.Messaging;

namespace MessageBrokerDemo.Consumer;

public sealed record JobNotificationMessage(long Id, string TenantId, string CorrelationId, string Status, DateTimeOffset CompletedAtUtc);

public sealed class JobNotificationHandler(ILogger<JobNotificationHandler> logger) : IMessageHandler
{
    public Task HandleAsync(string routingKey, string payload, CancellationToken cancellationToken)
    {
        var message = JsonSerializer.Deserialize<JobNotificationMessage>(payload)
            ?? throw new InvalidOperationException($"Invalid {routingKey} payload.");

        /*TODO
         * perform business logic on the notification message, e.g. update a database, send an email, etc.
         */

        logger.LogInformation(
            "Job {JobId} notification {RoutingKey}: CorrelationId {CorrelationId}, Status {Status}.",
            message.Id, routingKey, message.CorrelationId, message.Status);

        return Task.CompletedTask;
    }
}
