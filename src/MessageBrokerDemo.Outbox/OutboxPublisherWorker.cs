using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MessageBrokerDemo.Broker.Messaging;
using MessageBrokerDemo.Data.Persistence;
using MessageBrokerDemo.Model.Job;
using MessageBrokerDemo.Model.Messaging;
using MessageBrokerDemo.Model.Outbox;

namespace MessageBrokerDemo.Outbox;

public sealed class OutboxPublisherOptions
{
    public const string SectionName = "OutboxPublisher";

    public int BatchSize { get; set; } = 50;
    public int PollIntervalSeconds { get; set; } = 5;
    public int MaxRetries { get; set; } = 5;
    public int LockSeconds { get; set; } = 30;
}

/*
 * Implementation of the Outbox pattern for reliable message publishing.
 * use explicit LOCK hint to show case the dual writes problem, and how to solve it with the outbox pattern.
 * TODO: add MassTransit integration to show how to use the outbox pattern with MassTransit.
 */
public sealed class OutboxPublisherWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<OutboxPublisherOptions> options,
    ILogger<OutboxPublisherWorker> logger) : BackgroundService
{
    private readonly OutboxPublisherOptions _options = options.Value;
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PublishBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Outbox publish cycle failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(_options.PollIntervalSeconds), stoppingToken);
        }
    }

    private async Task StartQueuedJobsAsync(WCMSDBContext db, CancellationToken cancellationToken)
    {
        /** 
         * Select a batch of queued jobs and mark them as processing. 
         * Lock the selected rows for update to prevent
         * other processes from selecting the same rows.
         */
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

        var jobs = await db.Jobs
            .FromSqlInterpolated($"""
                SELECT * FROM jobs
                WHERE "Status" = {nameof(JobStatus.Queued)}
                ORDER BY "CreatedAtUtc"
                LIMIT {_options.BatchSize}
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(cancellationToken);

        if (jobs.Count == 0)
        {
            await tx.CommitAsync(cancellationToken);
            return;
        }

        var startedAt = DateTimeOffset.UtcNow;
        foreach (var job in jobs)
        {
            job.Status = JobStatus.Processing;
            job.StartedAtUtc = startedAt;

            db.OutboxEvents.Add(new OutboxEvent
            {
                EventType = MessageTopic.JobProcess.ToTopicName(),
                Payload = JsonSerializer.Serialize(new
                {
                    job.Id,
                    job.TenantId,
                    job.CorrelationId,
                    job.StagedObjectPath
                }),
                OccurredAtUtc = startedAt
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        logger.LogInformation("Started {Count} queued job(s).", jobs.Count);
    }

    private async Task PublishBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WCMSDBContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IMessageBusPublisher>();

        await StartQueuedJobsAsync(db, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var lockUntil = now.AddSeconds(_options.LockSeconds);

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

        /*
         * Select a batch of outbox events that are not yet published, have not exceeded the max retry count,
         * and are not currently locked by another process. Lock the selected rows for update to prevent
         * other processes from selecting the same rows.
         */
        var batch = await db.OutboxEvents
            .FromSqlInterpolated($"""
                SELECT * FROM outbox_events
                WHERE "PublishedAtUtc" IS NULL
                  AND "RetryCount" < {_options.MaxRetries}
                  AND ("LockedUntilUtc" IS NULL OR "LockedUntilUtc" < {now})
                ORDER BY "OccurredAtUtc"
                LIMIT {_options.BatchSize}
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(cancellationToken);

        if (batch.Count == 0)
        {
            await tx.CommitAsync(cancellationToken);
            return;
        }

        /*
         * Lock the selected rows by setting the LockedUntilUtc column to a future timestamp.
         * This prevents other processes from selecting the same rows until the lock expires.
         */
        foreach (var evt in batch)
        {
            evt.LockedUntilUtc = lockUntil;
        }
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        var published = 0;
        var failed = 0;

        /*
         * NOTE:
         * This is the part preventing the dual writes problem, 
         * Message publish and the table update are in the same transaction, 
         * so if the message publish fails, the table update will not be committed.      
         * 
         * Publish each event in the batch. If publishing fails, increment the retry count and record the error.
         */
        foreach (var evt in batch)
        {
            try
            {
                await publisher.PublishAsync(evt.EventType, evt.Payload, cancellationToken);
                evt.PublishedAtUtc = DateTimeOffset.UtcNow;
                evt.LastError = null;
                published++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                evt.RetryCount++;
                evt.LastError = ex.Message.Length > 1024 ? ex.Message[..1024] : ex.Message;
                failed++;
                logger.LogWarning(ex, "Failed to publish outbox event {EventId}.", evt.Id);
            }
            finally
            {
                evt.LockedUntilUtc = null;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Outbox batch: {Published} published, {Failed} failed.", published, failed);
    }
}
