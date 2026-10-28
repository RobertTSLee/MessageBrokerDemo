namespace MessageBrokerDemo.Model.Job;

public class Job
{
    public long Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string SourceSystem { get; set; } = string.Empty;
    public string StagedObjectPath { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public JobStatus Status { get; set; } = JobStatus.Queued;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? StartedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
}

public enum JobStatus
{
    Queued = 0,
    Processing = 1,
    Completed = 2,
    PartialFailed = 3,
    Failed = 4
}
