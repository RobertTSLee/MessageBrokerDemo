using Microsoft.AspNetCore.Http;

namespace MessageBrokerDemo.Model.Api;

public class UploadRequest
{
    /*
     * The tenant ID of the client uploading the file.
     */
    public string TenantId { get; set; } = string.Empty;    
    public IFormFile? File { get; set; }
}
