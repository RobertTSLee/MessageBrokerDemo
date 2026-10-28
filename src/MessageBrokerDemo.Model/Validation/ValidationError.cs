namespace MessageBrokerDemo.Model.Validation;

public class ValidationError
{
    public long Id { get; set; }
    public long JobId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? ContentId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
