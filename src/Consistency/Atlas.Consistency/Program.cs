using Atlas.Consistency;
using StackExchange.Redis;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System;

var b = Host.CreateApplicationBuilder(args);
b.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(Environment.GetEnvironmentVariable("REDIS") ?? "redis:6379"));
b.Services.AddHostedService<DebeziumWatermarkTracker>(); // updates per-region watermarks
b.Build().Run();
