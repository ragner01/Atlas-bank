using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Mvc;

var b = WebApplication.CreateBuilder(args);
b.Services.AddSingleton(new BlobServiceClient(Environment.GetEnvironmentVariable("BLOB_CONN") ?? "UseDevelopmentStorage=true"));
var app = b.Build();

app.MapGet("/health", () => Results.Ok(new { status="ok" }));

// POST /recon/network — upload a (simulated) scheme settlement CSV and mark matched/variance
// CSV headers: rrn,minor,currency,merchant_id,result
app.MapPost("/recon/network", async ([FromServices] BlobServiceClient blob, IFormFile file, CancellationToken ct) =>
{
    var cont = blob.GetBlobContainerClient("recon-in");
    await cont.CreateIfNotExistsAsync(cancellationToken: ct);
    var name = $"network_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
    await cont.UploadBlobAsync(name, file.OpenReadStream(), ct);
    return Results.Accepted(new { uploaded = name });
});

app.Run();
