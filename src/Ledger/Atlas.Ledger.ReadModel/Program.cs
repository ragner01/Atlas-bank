using Atlas.Ledger.ReadModel;
using StackExchange.Redis;

var b = Host.CreateApplicationBuilder(args);

b.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(Environment.GetEnvironmentVariable("REDIS") ?? "redis:6379"));
b.Services.AddHostedService<LedgerProjectionWorker>();

b.Build().Run();
