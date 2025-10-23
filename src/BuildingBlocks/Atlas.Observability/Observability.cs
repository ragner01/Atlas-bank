using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using OpenTelemetry.Exporter;
using Serilog;
using Atlas.Common.ValueObjects;

namespace Atlas.Observability;

/// <summary>
/// OpenTelemetry configuration and setup
/// </summary>
public static class OpenTelemetryExtensions
{
    public static IServiceCollection AddAtlasObservability(this IServiceCollection services, Action<ObservabilityOptions> configure)
    {
        var options = new ObservabilityOptions();
        configure(options);

        services.AddSingleton(options);

        // Configure Serilog
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(options.Configuration)
            .Enrich.WithProperty("ServiceName", options.ServiceName)
            .Enrich.WithProperty("ServiceVersion", options.ServiceVersion)
            .Enrich.WithProperty("Environment", options.Environment)
            .WriteTo.Console()
            .WriteTo.File($"logs/{options.ServiceName}-.log", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        services.AddLogging(builder => builder.AddSerilog());

        // OpenTelemetry Tracing
        services.AddOpenTelemetry()
            .WithTracing(tracerProviderBuilder =>
            {
                tracerProviderBuilder
                    .SetResourceBuilder(ResourceBuilder.CreateDefault()
                        .AddService(serviceName: options.ServiceName, serviceVersion: options.ServiceVersion)
                        .AddAttributes(new Dictionary<string, object>
                        {
                            ["deployment.environment"] = options.Environment,
                            ["service.namespace"] = "atlas-bank"
                        }))
                    .AddAspNetCoreInstrumentation(aspNetOptions =>
                    {
                        aspNetOptions.RecordException = true;
                        aspNetOptions.EnrichWithHttpRequest = (activity, httpRequest) =>
                        {
                            activity.SetTag("tenant.id", ExtractTenantId(httpRequest));
                        };
                    })
                    .AddHttpClientInstrumentation()
                    .AddEntityFrameworkCoreInstrumentation();

                if (options.JaegerEndpoint != null)
                {
                    tracerProviderBuilder.AddJaegerExporter(jaegerOptions =>
                    {
                        jaegerOptions.Endpoint = new Uri(options.JaegerEndpoint);
                    });
                }
            })
            .WithMetrics(metricsProviderBuilder =>
            {
                metricsProviderBuilder
                    .SetResourceBuilder(ResourceBuilder.CreateDefault()
                        .AddService(serviceName: options.ServiceName, serviceVersion: options.ServiceVersion))
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();

                if (options.PrometheusEndpoint != null)
                {
                    metricsProviderBuilder.AddPrometheusExporter();
                }
            });

        return services;
    }

    private static string ExtractTenantId(HttpRequest httpRequest)
    {
        // Try to get tenant ID from various sources
        if (httpRequest.Headers.TryGetValue("X-Tenant-Id", out var headerValue))
            return headerValue.FirstOrDefault() ?? "unknown";

        var host = httpRequest.Host.Host;
        if (host.Contains('.'))
        {
            var subdomain = host.Split('.')[0];
            if (subdomain != "www" && subdomain != "api")
                return subdomain;
        }

        return "unknown";
    }
}

/// <summary>
/// Health check extensions
/// </summary>
public static class HealthCheckExtensions
{
    public static IServiceCollection AddAtlasHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database")
            .AddCheck<MessageBrokerHealthCheck>("message-broker")
            .AddCheck<ExternalServiceHealthCheck>("external-services");

        return services;
    }
}

/// <summary>
/// Database health check
/// </summary>
public class DatabaseHealthCheck : IHealthCheck
{
    private readonly DbContext _context;
    private readonly ILogger<DatabaseHealthCheck> _logger;

    public DatabaseHealthCheck(DbContext context, ILogger<DatabaseHealthCheck> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // Simple database connectivity check
            var canConnect = await _context.Database.CanConnectAsync(cancellationToken);
            if (!canConnect)
            {
                return HealthCheckResult.Unhealthy("Database is not accessible");
            }
            return HealthCheckResult.Healthy("Database is accessible");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database health check failed");
            return HealthCheckResult.Unhealthy("Database is not accessible", ex);
        }
    }
}

/// <summary>
/// Message broker health check
/// </summary>
public class MessageBrokerHealthCheck : IHealthCheck
{
    private readonly ILogger<MessageBrokerHealthCheck> _logger;

    public MessageBrokerHealthCheck(ILogger<MessageBrokerHealthCheck> logger)
    {
        _logger = logger;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        // TODO: Implement actual Kafka/Event Hubs health check
        return Task.FromResult(HealthCheckResult.Healthy("Message broker is accessible"));
    }
}

/// <summary>
/// External services health check
/// </summary>
public class ExternalServiceHealthCheck : IHealthCheck
{
    private readonly ILogger<ExternalServiceHealthCheck> _logger;

    public ExternalServiceHealthCheck(ILogger<ExternalServiceHealthCheck> logger)
    {
        _logger = logger;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        // TODO: Implement actual external service health checks
        return Task.FromResult(HealthCheckResult.Healthy("External services are accessible"));
    }
}

/// <summary>
/// Observability options
/// </summary>
public class ObservabilityOptions
{
    public string ServiceName { get; set; } = "atlas-bank";
    public string ServiceVersion { get; set; } = "1.0.0";
    public string Environment { get; set; } = "Development";
    public IConfiguration Configuration { get; set; } = null!;
    public string? JaegerEndpoint { get; set; }
    public string? PrometheusEndpoint { get; set; }
}

/// <summary>
/// Activity extensions for tracing
/// </summary>
public static class ActivityExtensions
{
    public static void SetTenantId(this Activity activity, TenantId tenantId)
    {
        activity.SetTag("tenant.id", tenantId.Value);
    }

    public static void SetUserId(this Activity activity, string userId)
    {
        activity.SetTag("user.id", userId);
    }

    public static void SetTransactionId(this Activity activity, string transactionId)
    {
        activity.SetTag("transaction.id", transactionId);
    }

    public static void SetAmount(this Activity activity, Money amount)
    {
        activity.SetTag("amount.value", amount.Value);
        activity.SetTag("amount.currency", amount.Currency.Code);
    }
}
