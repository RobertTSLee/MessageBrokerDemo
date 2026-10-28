namespace MessageBrokerDemo.Model.Content;

public class ContentItem
{
    public long Id { get; set; }
    public long JobId { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string MetadataJson { get; set; } = "{}";
    public string? MediaObjectPath { get; set; }
}
