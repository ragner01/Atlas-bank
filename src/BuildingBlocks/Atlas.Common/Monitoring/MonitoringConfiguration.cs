using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Http;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using OpenTelemetry.Logs;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.OpenTelemetry;

namespace AtlasBank.Common.Monitoring;

/// <summary>
/// Comprehensive monitoring configuration for AtlasBank services
/// </summary>
public static class MonitoringConfiguration
{
    /// <summary>
    /// Configure OpenTelemetry tracing, metrics, and logging
    /// </summary>
    public static IServiceCollection AddMonitoring(this IServiceCollection services, string serviceName, string serviceVersion = "1.0.0")
    {
        // Add OpenTelemetry services
        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(serviceName: serviceName, serviceVersion: serviceVersion)
                .AddAttributes(new Dictionary<string, object>
                {
                    ["deployment.environment"] = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
                    ["service.instance.id"] = Environment.MachineName,
                    ["service.namespace"] = "atlasbank"
                }))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation(options =>
                {
                    options.RecordException = true;
                    options.EnrichWithHttpRequest = (activity, httpRequest) =>
                    {
                        activity.SetTag("http.request.body.size", httpRequest.ContentLength);
                        activity.SetTag("http.request.user_agent", httpRequest.Headers.UserAgent.ToString());
                        activity.SetTag("http.request.client_ip", GetClientIP(httpRequest.HttpContext));
                    };
                    options.EnrichWithHttpResponse = (activity, httpResponse) =>
                    {
                        activity.SetTag("http.response.body.size", httpResponse.ContentLength);
                    };
                })
                .AddHttpClientInstrumentation()
                .AddEntityFrameworkCoreInstrumentation()
                .AddSqlClientInstrumentation()
                .AddRedisInstrumentation()
                .AddKafkaInstrumentation()
                .AddJaegerExporter())
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddProcessInstrumentation()
                .AddPrometheusExporter());

        // Add health checks
        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database")
            .AddCheck<RedisHealthCheck>("redis")
            .AddCheck<KafkaHealthCheck>("kafka")
            .AddCheck<ExternalServiceHealthCheck>("external-services");

        return services;
    }

    /// <summary>
    /// Configure Serilog with OpenTelemetry integration
    /// </summary>
    public static void ConfigureSerilogWithOpenTelemetry(string serviceName, string serviceVersion = "1.0.0")
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Service", serviceName)
            .Enrich.WithProperty("Version", serviceVersion)
            .Enrich.WithProperty("Environment", Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production")
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
            .WriteTo.OpenTelemetry(options =>
            {
                options.Endpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT") ?? "http://localhost:4317";
                options.Protocol = Serilog.Sinks.OpenTelemetry.OtlpProtocol.Grpc;
                options.ResourceAttributes = new Dictionary<string, object>
                {
                    ["service.name"] = serviceName,
                    ["service.version"] = serviceVersion,
                    ["deployment.environment"] = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"
                };
            })
            .CreateLogger();
    }

    /// <summary>
    /// Use monitoring middleware
    /// </summary>
    public static IApplicationBuilder UseMonitoring(this IApplicationBuilder app)
    {
        // Use health checks
        app.UseHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            ResponseWriter = async (context, report) =>
            {
                context.Response.ContentType = "application/json";
                var response = new
                {
                    status = report.Status.ToString(),
                    checks = report.Entries.Select(entry => new
                    {
                        name = entry.Key,
                        status = entry.Value.Status.ToString(),
                        duration = entry.Value.Duration.TotalMilliseconds,
                        description = entry.Value.Description,
                        data = entry.Value.Data
                    }),
                    totalDuration = report.TotalDuration.TotalMilliseconds
                };
                await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response));
            }
        });

        // Use metrics endpoint
        app.UseOpenTelemetryPrometheusScrapingEndpoint();

        return app;
    }

    private static string GetClientIP(HttpContext context)
    {
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }
        
        var realIP = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIP))
        {
            return realIP;
        }
        
        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}

/// <summary>
/// Database health check
/// </summary>
public class DatabaseHealthCheck : Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck
{
    private readonly string _connectionString;

    public DatabaseHealthCheck(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult> CheckHealthAsync(
        Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext context, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = new Npgsql.NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            
            using var command = new Npgsql.NpgsqlCommand("SELECT 1", connection);
            await command.ExecuteScalarAsync(cancellationToken);
            
            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("Database is healthy");
        }
        catch (Exception ex)
        {
            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy("Database is unhealthy", ex);
        }
    }
}

/// <summary>
/// Redis health check
/// </summary>
public class RedisHealthCheck : Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck
{
    private readonly StackExchange.Redis.IConnectionMultiplexer _redis;

    public RedisHealthCheck(StackExchange.Redis.IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult> CheckHealthAsync(
        Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext context, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var database = _redis.GetDatabase();
            await database.PingAsync();
            
            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("Redis is healthy");
        }
        catch (Exception ex)
        {
            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy("Redis is unhealthy", ex);
        }
    }
}

/// <summary>
/// Kafka health check
/// </summary>
public class KafkaHealthCheck : Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck
{
    private readonly string _bootstrapServers;

    public KafkaHealthCheck(string bootstrapServers)
    {
        _bootstrapServers = bootstrapServers;
    }

    public async Task<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult> CheckHealthAsync(
        Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext context, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var adminClient = new Confluent.Kafka.AdminClientBuilder(new Confluent.Kafka.AdminClientConfig
            {
                BootstrapServers = _bootstrapServers
            }).Build();

            var metadata = adminClient.GetMetadata(TimeSpan.FromSeconds(5));
            
            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("Kafka is healthy");
        }
        catch (Exception ex)
        {
            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy("Kafka is unhealthy", ex);
        }
    }
}

/// <summary>
/// External service health check
/// </summary>
public class ExternalServiceHealthCheck : Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck
{
    private readonly HttpClient _httpClient;

    public ExternalServiceHealthCheck(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult> CheckHealthAsync(
        Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext context, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/health", cancellationToken);
            response.EnsureSuccessStatusCode();
            
            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("External services are healthy");
        }
        catch (Exception ex)
        {
            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy("External services are unhealthy", ex);
        }
    }
}

