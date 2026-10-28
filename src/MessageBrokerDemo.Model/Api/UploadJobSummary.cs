using MessageBrokerDemo.Model.Job;

namespace MessageBrokerDemo.Model.Api;

public sealed record UploadJobSummary(
    long JobId,
    string TenantId,
    string StagedObjectPath,
    string CorrelationId,
    JobStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt
);
