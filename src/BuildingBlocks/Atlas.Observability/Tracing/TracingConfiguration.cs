using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetry.Exporter;
using Serilog;
using Serilog.Context;

namespace AtlasBank.BuildingBlocks.Tracing;

/// <summary>
/// Comprehensive distributed tracing configuration
/// </summary>
public static class TracingConfiguration
{
    public static void AddAtlasTracing(this IServiceCollection services, IConfiguration configuration)
    {
        var tracingConfig = configuration.GetSection("Tracing").Get<TracingConfiguration>() ?? new TracingConfiguration();
        
        services.AddOpenTelemetry()
            .WithTracing(builder =>
            {
                builder
                    .SetResourceBuilder(ResourceBuilder.CreateDefault()
                        .AddService(tracingConfig.ServiceName, tracingConfig.ServiceVersion)
                        .AddAttributes(new Dictionary<string, object>
                        {
                            ["deployment.environment"] = tracingConfig.Environment,
                            ["service.namespace"] = tracingConfig.Namespace
                        }))
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.RecordException = true;
                        options.EnrichWithHttpRequest = (activity, httpRequest) =>
                        {
                            activity.SetTag("http.request.header.user_agent", httpRequest.Headers.UserAgent.ToString());
                            activity.SetTag("http.request.header.x_forwarded_for", httpRequest.Headers["X-Forwarded-For"].ToString());
                        };
                        options.EnrichWithHttpResponse = (activity, httpResponse) =>
                        {
                            activity.SetTag("http.response.header.content_type", httpResponse.Headers.ContentType.ToString());
                        };
                    })
                    .AddHttpClientInstrumentation(options =>
                    {
                        options.RecordException = true;
                        options.EnrichWithHttpRequestMessage = (activity, httpRequestMessage) =>
                        {
                            activity.SetTag("http.client.method", httpRequestMessage.Method.Method);
                            activity.SetTag("http.client.url", httpRequestMessage.RequestUri?.ToString());
                        };
                    })
                    .AddNpgsql()
                    .AddRedis()
                    .AddKafka()
                    .AddSource("AtlasBank.Business")
                    .AddSource("AtlasBank.USSD")
                    .AddSource("AtlasBank.Agent")
                    .AddSource("AtlasBank.Offline");

                if (tracingConfig.ExportToJaeger)
                {
                    builder.AddJaegerExporter(options =>
                    {
                        options.AgentHost = tracingConfig.JaegerHost;
                        options.AgentPort = tracingConfig.JaegerPort;
                        options.ExportProcessorType = ExportProcessorType.Batch;
                    });
                }

                if (tracingConfig.ExportToOtlp)
                {
                    builder.AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri(tracingConfig.OtlpEndpoint);
                        options.ExportProcessorType = ExportProcessorType.Batch;
                    });
                }
            })
            .WithMetrics(builder =>
            {
                builder
                    .SetResourceBuilder(ResourceBuilder.CreateDefault()
                        .AddService(tracingConfig.ServiceName, tracingConfig.ServiceVersion))
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddProcessInstrumentation()
                    .AddMeter("AtlasBank.Metrics");

                if (tracingConfig.ExportToPrometheus)
                {
                    builder.AddPrometheusExporter();
                }
            })
            .WithLogging(builder =>
            {
                builder
                    .SetResourceBuilder(ResourceBuilder.CreateDefault()
                        .AddService(tracingConfig.ServiceName, tracingConfig.ServiceVersion))
                    .AddSerilog();

                if (tracingConfig.ExportToOtlp)
                {
                    builder.AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri(tracingConfig.OtlpEndpoint);
                    });
                }
            });
    }

    public static void UseAtlasTracing(this WebApplication app)
    {
        // Add correlation ID middleware
        app.Use(async (context, next) =>
        {
            var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault() ?? Guid.NewGuid().ToString();
            context.Response.Headers["X-Correlation-ID"] = correlationId;
            
            using (LogContext.PushProperty("CorrelationId", correlationId))
            {
                await next();
            }
        });

        // Add tracing middleware
        app.Use(async (context, next) =>
        {
            var activity = Activity.Current;
            if (activity != null)
            {
                activity.SetTag("service.name", "AtlasBank");
                activity.SetTag("service.version", "1.0.0");
                activity.SetTag("deployment.environment", Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development");
            }
            
            await next();
        });
    }
}

/// <summary>
/// Tracing configuration model
/// </summary>
public class TracingConfiguration
{
    public string ServiceName { get; set; } = "AtlasBank";
    public string ServiceVersion { get; set; } = "1.0.0";
    public string Environment { get; set; } = "Development";
    public string Namespace { get; set; } = "AtlasBank";
    public bool ExportToJaeger { get; set; } = true;
    public string JaegerHost { get; set; } = "localhost";
    public int JaegerPort { get; set; } = 14268;
    public bool ExportToOtlp { get; set; } = false;
    public string OtlpEndpoint { get; set; } = "http://localhost:4317";
    public bool ExportToPrometheus { get; set; } = true;
}

/// <summary>
/// Business activity source for custom tracing
/// </summary>
public static class BusinessActivitySource
{
    private static readonly ActivitySource Source = new("AtlasBank.Business");

    public static Activity? StartTransactionActivity(string transactionId, string type, string sourceAccount, string destinationAccount)
    {
        var activity = Source.StartActivity("transaction.process");
        if (activity != null)
        {
            activity.SetTag("transaction.id", transactionId);
            activity.SetTag("transaction.type", type);
            activity.SetTag("transaction.source_account", sourceAccount);
            activity.SetTag("transaction.destination_account", destinationAccount);
            activity.SetTag("transaction.status", "processing");
        }
        return activity;
    }

    public static Activity? StartUssdActivity(string sessionId, string msisdn, string step)
    {
        var activity = Source.StartActivity("ussd.session");
        if (activity != null)
        {
            activity.SetTag("ussd.session_id", sessionId);
            activity.SetTag("ussd.msisdn", msisdn);
            activity.SetTag("ussd.step", step);
        }
        return activity;
    }

    public static Activity? StartAgentActivity(string agentCode, string operationType, string msisdn)
    {
        var activity = Source.StartActivity("agent.operation");
        if (activity != null)
        {
            activity.SetTag("agent.code", agentCode);
            activity.SetTag("agent.operation_type", operationType);
            activity.SetTag("agent.msisdn", msisdn);
        }
        return activity;
    }

    public static Activity? StartOfflineActivity(string deviceId, string operationType)
    {
        var activity = Source.StartActivity("offline.operation");
        if (activity != null)
        {
            activity.SetTag("offline.device_id", deviceId);
            activity.SetTag("offline.operation_type", operationType);
        }
        return activity;
    }
}

/// <summary>
/// Custom tracing middleware
/// </summary>
public class CustomTracingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CustomTracingMiddleware> _logger;

    public CustomTracingMiddleware(RequestDelegate next, ILogger<CustomTracingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var activity = Activity.Current;
        if (activity != null)
        {
            // Add custom tags based on request path
            var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";
            
            if (path.Contains("/ussd"))
            {
                activity.SetTag("service.component", "ussd");
                activity.SetTag("service.operation", "ussd_request");
            }
            else if (path.Contains("/agent"))
            {
                activity.SetTag("service.component", "agent");
                activity.SetTag("service.operation", "agent_request");
            }
            else if (path.Contains("/offline"))
            {
                activity.SetTag("service.component", "offline");
                activity.SetTag("service.operation", "offline_request");
            }
            else if (path.Contains("/payments"))
            {
                activity.SetTag("service.component", "payments");
                activity.SetTag("service.operation", "payment_request");
            }
            else if (path.Contains("/ledger"))
            {
                activity.SetTag("service.component", "ledger");
                activity.SetTag("service.operation", "ledger_request");
            }

            // Add request metadata
            activity.SetTag("http.method", context.Request.Method);
            activity.SetTag("http.url", context.Request.GetDisplayUrl());
            activity.SetTag("http.user_agent", context.Request.Headers.UserAgent.ToString());
            activity.SetTag("http.client_ip", GetClientIp(context));
        }

        await _next(context);
    }

    private string GetClientIp(HttpContext context)
    {
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp))
        {
            return realIp;
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}

/// <summary>
/// Tracing utilities
/// </summary>
public static class TracingUtilities
{
    /// <summary>
    /// Creates a span for database operations
    /// </summary>
    public static Activity? StartDatabaseSpan(string operation, string table, string? query = null)
    {
        var activity = Activity.Current?.Source.StartActivity($"db.{operation}");
        if (activity != null)
        {
            activity.SetTag("db.operation", operation);
            activity.SetTag("db.table", table);
            if (!string.IsNullOrEmpty(query))
            {
                activity.SetTag("db.query", query);
            }
        }
        return activity;
    }

    /// <summary>
    /// Creates a span for external API calls
    /// </summary>
    public static Activity? StartExternalApiSpan(string service, string endpoint, string method)
    {
        var activity = Activity.Current?.Source.StartActivity($"external.{service}.{method}");
        if (activity != null)
        {
            activity.SetTag("external.service", service);
            activity.SetTag("external.endpoint", endpoint);
            activity.SetTag("external.method", method);
        }
        return activity;
    }

    /// <summary>
    /// Creates a span for Redis operations
    /// </summary>
    public static Activity? StartRedisSpan(string operation, string key)
    {
        var activity = Activity.Current?.Source.StartActivity($"redis.{operation}");
        if (activity != null)
        {
            activity.SetTag("redis.operation", operation);
            activity.SetTag("redis.key", key);
        }
        return activity;
    }

    /// <summary>
    /// Creates a span for Kafka operations
    /// </summary>
    public static Activity? StartKafkaSpan(string operation, string topic, string? partition = null)
    {
        var activity = Activity.Current?.Source.StartActivity($"kafka.{operation}");
        if (activity != null)
        {
            activity.SetTag("kafka.operation", operation);
            activity.SetTag("kafka.topic", topic);
            if (!string.IsNullOrEmpty(partition))
            {
                activity.SetTag("kafka.partition", partition);
            }
        }
        return activity;
    }

    /// <summary>
    /// Adds error information to the current span
    /// </summary>
    public static void RecordError(Exception exception)
    {
        var activity = Activity.Current;
        if (activity != null)
        {
            activity.SetTag("error", true);
            activity.SetTag("error.message", exception.Message);
            activity.SetTag("error.type", exception.GetType().Name);
            activity.SetStatus(ActivityStatusCode.Error, exception.Message);
        }
    }

    /// <summary>
    /// Adds success information to the current span
    /// </summary>
    public static void RecordSuccess(string? message = null)
    {
        var activity = Activity.Current;
        if (activity != null)
        {
            activity.SetTag("success", true);
            if (!string.IsNullOrEmpty(message))
            {
                activity.SetTag("success.message", message);
            }
            activity.SetStatus(ActivityStatusCode.Ok);
        }
    }
}
