using MessageBrokerDemo.Model.Job;
using MessageBrokerDemo.Data.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MessageBrokerDemo.Repository;

public interface IUploadRepository
{
    Task AddAsync(Job uploadJob, CancellationToken cancellationToken);
    Task<Job?> GetAsync(long uploadJobId, CancellationToken cancellationToken);
    Task<Job?> GetNextQueuedAsync(CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public class UploadRepository(
    WCMSDBContext dbContext
) : IUploadRepository
{
    public Task AddAsync(Job uploadJob, CancellationToken cancellationToken) =>
    dbContext.Jobs.AddAsync(uploadJob, cancellationToken).AsTask();

    public Task<Job?> GetAsync(long uploadJobId, CancellationToken cancellationToken) =>
        dbContext.Jobs.FirstOrDefaultAsync(x => x.Id == uploadJobId, cancellationToken);

    public Task<Job?> GetNextQueuedAsync(CancellationToken cancellationToken) =>
        dbContext.Jobs
            .Where(x => x.Status == JobStatus.Queued)
            .OrderBy(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
