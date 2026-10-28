using Microsoft.AspNetCore.Mvc;

using MessageBrokerDemo.Model.Api;
using MessageBrokerDemo.Data;
using MessageBrokerDemo.Repository;
using MessageBrokerDemo.Service;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
#region core services
builder.Services.AddAntiforgery();
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
#endregion

#region singleton services
#endregion

#region scoped services
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddRepositories();
builder.Services.AddServices();
#endregion

#region transient services
#endregion


// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

/*
 * Minimum API endpoints to handle file upload requests.
 */
app.MapPost("/api/upload", async (
    [FromForm] UploadRequest request,
    IUploadService uploadService,
    IWebHostEnvironment environment,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    // TODO: Validate the request (e.g., check for null or empty fields, validate file type and size, etc.)
    // return BadRequest if validation fails.

    // TODO: MOCK the file upload for now, in a real scenario,
    // you would save the uploaded file to the uploadsRoot directory, in Object Storage or a Blob Storage service,
    // and use the path to the uploaded file in the import request.
    var uploadsRoot = Path.Combine(environment.ContentRootPath, "App_Data", "imports");
    Directory.CreateDirectory(uploadsRoot);
    var stagedFileName = $"{Guid.NewGuid():N}.zip";
    var stagedObjectPath = Path.Combine(uploadsRoot, stagedFileName);

    var importRequest = new UploadRequestRecord(
        request.TenantId,
        stagedObjectPath);

    // Queue the upload job and return a 202 Accepted response with the location of the upload job status endpoint.
    var result = await uploadService.QueueAsync(importRequest, cancellationToken);

    return Results.Accepted($"/api/imports/{result.JobId}", result);
})
.Accepts<UploadRequest>("multipart/form-data")
.DisableAntiforgery(); // TODO: by pass for demo purposes, in a real scenario, you would want to enable CSRF protection for this endpoint.

/*
 * Endpoint to retrieve the status of an upload job by its ID.
 */
app.MapGet("/api/upload/{importJobId:long}", async (long importJobId, IUploadService uploadService, CancellationToken cancellationToken) =>
{
    var result = await uploadService.GetAsync(importJobId, cancellationToken);
    return result is null ? Results.NotFound() : Results.Ok(result);
});

app.MapSwagger();

app.Run();

// Make the implicit Program class public so test projects can access it
public partial class Program;