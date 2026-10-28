using MessageBrokerDemo.Model.Api;
using MessageBrokerDemo.Model.Job;
using MessageBrokerDemo.Repository;

namespace MessageBrokerDemo.Service;


public interface IUploadService
{
    Task<UploadJobSummary> QueueAsync(UploadRequestRecord request, CancellationToken cancellationToken);
    Task<UploadJobSummary?> GetAsync(long importJobId, CancellationToken cancellationToken);
}

public class UploadService(
    IUploadRepository uploadRepo
) : IUploadService
{
    public async Task<UploadJobSummary> QueueAsync(UploadRequestRecord request, CancellationToken cancellationToken)
    {
        // NOTE: use GUID version 7 for correlation id to ensure uniqueness and sortability
        var job = new Job()
        {
            TenantId = request.TenantId,
            StagedObjectPath = request.StagedObjectPath,
            CorrelationId = Guid.CreateVersion7().ToString(),
            Status = JobStatus.Queued,
            CreatedAtUtc = DateTime.UtcNow,
            StartedAtUtc = null,
            CompletedAtUtc = null
        };

        await uploadRepo.AddAsync(job, cancellationToken);
        await uploadRepo.SaveChangesAsync(cancellationToken);

        // todo: publish an event to the outbox table to notify other services that a new upload job has been queued.

        return ToSummary(job);
    }

    public async Task<UploadJobSummary?> GetAsync(long importJobId, CancellationToken cancellationToken)
    {
        var importJob = await uploadRepo
            .GetAsync(importJobId, cancellationToken);

        return importJob is null ? null : ToSummary(importJob);
    }

    private static UploadJobSummary ToSummary(Job importJob) =>
        new UploadJobSummary(
            JobId: importJob.Id,
            TenantId: importJob.TenantId,            
            StagedObjectPath: importJob.StagedObjectPath,
            Status: importJob.Status,
            CorrelationId: importJob.CorrelationId,
            CreatedAt: importJob.CreatedAtUtc,
            StartedAt: importJob.StartedAtUtc,
            CompletedAt: importJob.CompletedAtUtc
        );

}
