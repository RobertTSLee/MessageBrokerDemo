using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using MessageBrokerDemo.Broker.Messaging;
using MessageBrokerDemo.Data.Persistence;
using MessageBrokerDemo.Model.Job;
using MessageBrokerDemo.Model.Messaging;
using MessageBrokerDemo.Model.Outbox;

namespace MessageBrokerDemo.Worker;

public sealed record ProcessJobMessage(long Id, string TenantId, string CorrelationId, string StagedObjectPath);

public sealed class ProcessJobHandler(WCMSDBContext db, ILogger<ProcessJobHandler> logger) : IMessageHandler
{
    public async Task HandleAsync(string routingKey, string payload, CancellationToken cancellationToken)
    {
        var message = JsonSerializer.Deserialize<ProcessJobMessage>(payload)
            ?? throw new InvalidOperationException($"Invalid {routingKey} payload.");

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

        // idempotency check: ensure the job exists and is not already completed or failed 
        // use a select with FOR UPDATE to lock the row for processing, preventing concurrent processing of the same job
        var job = await db.Jobs
            .FromSqlInterpolated($"""
                SELECT * FROM jobs
                WHERE "Id" = {message.Id}
                FOR UPDATE
                """)
            .FirstOrDefaultAsync(cancellationToken);
        if (job is null)
        {
            logger.LogWarning("Job {JobId} not found, skipping.", message.Id);
            return;
        }

        if (job.Status is JobStatus.Completed or JobStatus.Failed)
        {
            logger.LogInformation("Job {JobId} already in terminal state {Status}, skipping.", job.Id, job.Status);
            return;
        }

        /*TODO:
         * Implement the actual processing logic for the job here. 
         * Validation, transformation, and any other business logic should be applied to the staged object.
         */
        try
        {
            // do somthing with the staged object at message.StagedObjectPath
            // mock async operation to simulate processing time
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);

            job.Status = JobStatus.Completed;
            logger.LogInformation("Processed staged file for job {JobId}.", job.Id);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            job.Status = JobStatus.Failed;
            logger.LogError(ex, "Failed to process staged file for job {JobId}.", job.Id);

            // TODO: Implement retry logic or move the job to a dead-letter queue if necessary.
        }

        job.CompletedAtUtc = DateTimeOffset.UtcNow;

        var topic = job.Status is JobStatus.Completed
            ? MessageTopic.JobCompletedNotification
            : MessageTopic.JobFailedNotification;

        // outbox event to be published by the OutboxPublisherWorker
        db.OutboxEvents.Add(new OutboxEvent
        {
            EventType = topic.ToTopicName(),
            Payload = JsonSerializer.Serialize(new
            {
                job.Id,
                job.TenantId,
                job.CorrelationId,
                Status = job.Status.ToString(),
                job.CompletedAtUtc
            }),
            OccurredAtUtc = job.CompletedAtUtc.Value
        });

        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
    }
}
