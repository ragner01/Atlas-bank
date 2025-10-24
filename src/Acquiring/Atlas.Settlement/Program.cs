using Atlas.Settlement;
using Azure.Storage.Blobs;

var b = Host.CreateApplicationBuilder(args);
b.Services.AddSingleton(new BlobServiceClient(Environment.GetEnvironmentVariable("BLOB_CONN") ?? "UseDevelopmentStorage=true"));
b.Services.AddHostedService<SettlementWorker>();
b.Build().Run();
