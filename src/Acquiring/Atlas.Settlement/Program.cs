using Atlas.Settlement;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Options;

var b = Host.CreateApplicationBuilder(args);

// Configure options
b.Services.Configure<SettlementOptions>(b.Configuration.GetSection(SettlementOptions.SectionName));
b.Services.AddSingleton<IValidateOptions<SettlementOptions>, SettlementOptionsValidator>();

// Add Blob service client
b.Services.AddSingleton(new BlobServiceClient(Environment.GetEnvironmentVariable("BLOB_CONN") ?? "UseDevelopmentStorage=true"));

// Add hosted service
b.Services.AddHostedService<SettlementWorker>();

b.Build().Run();
