namespace MessageBrokerDemo.Model.Api;

public sealed record UploadRequestRecord(
    string TenantId,
    string StagedObjectPath
);
