namespace MessageBrokerDemo.Model.Outbox;

public class OutboxEvent
{
    public long Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTimeOffset OccurredAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? PublishedAtUtc { get; set; }
    public int RetryCount { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset? LockedUntilUtc { get; set; }
}
