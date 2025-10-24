using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using Serilog.Context;

namespace AtlasBank.BuildingBlocks.Benchmarks;

/// <summary>
/// Comprehensive performance benchmarking utilities
/// </summary>
public static class PerformanceBenchmarks
{
    /// <summary>
    /// Measures the execution time of an operation
    /// </summary>
    public static async Task<T> MeasureAsync<T>(string operationName, Func<Task<T>> operation, ILogger? logger = null)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await operation();
            stopwatch.Stop();
            
            logger?.LogInformation("Performance: {OperationName} completed in {ElapsedMs}ms", 
                operationName, stopwatch.ElapsedMilliseconds);
            
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger?.LogError(ex, "Performance: {OperationName} failed after {ElapsedMs}ms", 
                operationName, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    /// <summary>
    /// Measures the execution time of a synchronous operation
    /// </summary>
    public static T Measure<T>(string operationName, Func<T> operation, ILogger? logger = null)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = operation();
            stopwatch.Stop();
            
            logger?.LogInformation("Performance: {OperationName} completed in {ElapsedMs}ms", 
                operationName, stopwatch.ElapsedMilliseconds);
            
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger?.LogError(ex, "Performance: {OperationName} failed after {ElapsedMs}ms", 
                operationName, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    /// <summary>
    /// Measures the execution time of an operation with detailed metrics
    /// </summary>
    public static async Task<PerformanceMetrics> MeasureDetailedAsync<T>(string operationName, Func<Task<T>> operation, ILogger? logger = null)
    {
        var stopwatch = Stopwatch.StartNew();
        var memoryBefore = GC.GetTotalMemory(false);
        var threadCountBefore = Process.GetCurrentProcess().Threads.Count;
        
        try
        {
            var result = await operation();
            stopwatch.Stop();
            
            var memoryAfter = GC.GetTotalMemory(false);
            var threadCountAfter = Process.GetCurrentProcess().Threads.Count;
            
            var metrics = new PerformanceMetrics
            {
                OperationName = operationName,
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                ElapsedTicks = stopwatch.ElapsedTicks,
                MemoryUsed = memoryAfter - memoryBefore,
                ThreadCountChange = threadCountAfter - threadCountBefore,
                Success = true,
                Timestamp = DateTime.UtcNow
            };
            
            logger?.LogInformation("Performance: {OperationName} completed in {ElapsedMs}ms, Memory: {MemoryUsed}bytes, Threads: {ThreadChange}", 
                operationName, stopwatch.ElapsedMilliseconds, metrics.MemoryUsed, metrics.ThreadCountChange);
            
            return metrics;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            var metrics = new PerformanceMetrics
            {
                OperationName = operationName,
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                ElapsedTicks = stopwatch.ElapsedTicks,
                Success = false,
                Error = ex.Message,
                Timestamp = DateTime.UtcNow
            };
            
            logger?.LogError(ex, "Performance: {OperationName} failed after {ElapsedMs}ms", 
                operationName, stopwatch.ElapsedMilliseconds);
            
            return metrics;
        }
    }
}

/// <summary>
/// Performance metrics model
/// </summary>
public class PerformanceMetrics
{
    public string OperationName { get; set; } = "";
    public long ElapsedMilliseconds { get; set; }
    public long ElapsedTicks { get; set; }
    public long MemoryUsed { get; set; }
    public int ThreadCountChange { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Performance benchmarking middleware
/// </summary>
public class PerformanceBenchmarkingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<PerformanceBenchmarkingMiddleware> _logger;
    private readonly PerformanceBenchmarkingConfiguration _config;

    public PerformanceBenchmarkingMiddleware(RequestDelegate next, ILogger<PerformanceBenchmarkingMiddleware> logger, PerformanceBenchmarkingConfiguration config)
    {
        _next = next;
        _logger = logger;
        _config = config;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!ShouldBenchmark(context))
        {
            await _next(context);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        var memoryBefore = GC.GetTotalMemory(false);
        var threadCountBefore = Process.GetCurrentProcess().Threads.Count;
        
        try
        {
            await _next(context);
            stopwatch.Stop();
            
            var memoryAfter = GC.GetTotalMemory(false);
            var threadCountAfter = Process.GetCurrentProcess().Threads.Count;
            
            var metrics = new PerformanceMetrics
            {
                OperationName = $"{context.Request.Method} {context.Request.Path}",
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                ElapsedTicks = stopwatch.ElapsedTicks,
                MemoryUsed = memoryAfter - memoryBefore,
                ThreadCountChange = threadCountAfter - threadCountBefore,
                Success = context.Response.StatusCode < 400,
                Timestamp = DateTime.UtcNow
            };
            
            LogPerformanceMetrics(metrics, context);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            var metrics = new PerformanceMetrics
            {
                OperationName = $"{context.Request.Method} {context.Request.Path}",
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                ElapsedTicks = stopwatch.ElapsedTicks,
                Success = false,
                Error = ex.Message,
                Timestamp = DateTime.UtcNow
            };
            
            LogPerformanceMetrics(metrics, context);
            throw;
        }
    }

    private bool ShouldBenchmark(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";
        
        // Skip health checks and metrics endpoints
        if (path.Contains("/health") || path.Contains("/metrics") || path.Contains("/swagger"))
        {
            return false;
        }

        // Check if endpoint is in benchmark configuration
        return _config.BenchmarkEndpoints.Any(endpoint => 
            path.StartsWith(endpoint.Path, StringComparison.OrdinalIgnoreCase) &&
            endpoint.Methods.Contains(context.Request.Method, StringComparer.OrdinalIgnoreCase));
    }

    private void LogPerformanceMetrics(PerformanceMetrics metrics, HttpContext context)
    {
        using (LogContext.PushProperty("PerformanceMetrics", JsonSerializer.Serialize(metrics)))
        {
            if (metrics.Success)
            {
                _logger.LogInformation("PERFORMANCE: {OperationName} completed in {ElapsedMs}ms, Memory: {MemoryUsed}bytes, Threads: {ThreadChange}", 
                    metrics.OperationName, metrics.ElapsedMilliseconds, metrics.MemoryUsed, metrics.ThreadCountChange);
            }
            else
            {
                _logger.LogError("PERFORMANCE: {OperationName} failed after {ElapsedMs}ms, Error: {Error}", 
                    metrics.OperationName, metrics.ElapsedMilliseconds, metrics.Error);
            }
        }
    }
}

/// <summary>
/// Performance benchmarking configuration
/// </summary>
public class PerformanceBenchmarkingConfiguration
{
    public bool Enabled { get; set; } = true;
    public List<PerformanceBenchmarkEndpoint> BenchmarkEndpoints { get; set; } = new();
    public long SlowOperationThresholdMs { get; set; } = 1000; // 1 second
    public long VerySlowOperationThresholdMs { get; set; } = 5000; // 5 seconds
}

/// <summary>
/// Performance benchmark endpoint configuration
/// </summary>
public class PerformanceBenchmarkEndpoint
{
    public string Path { get; set; } = "";
    public List<string> Methods { get; set; } = new();
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// Performance benchmarking controller
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class PerformanceBenchmarkController : ControllerBase
{
    private readonly ILogger<PerformanceBenchmarkController> _logger;

    public PerformanceBenchmarkController(ILogger<PerformanceBenchmarkController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Runs a performance benchmark for a specific operation
    /// </summary>
    [HttpPost("run")]
    public async Task<IActionResult> RunBenchmark([FromBody] BenchmarkRequest request)
    {
        try
        {
            var metrics = await PerformanceBenchmarks.MeasureDetailedAsync(
                request.OperationName, 
                async () => await SimulateOperation(request.OperationType, request.DurationMs),
                _logger);

            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Benchmark failed for operation: {OperationName}", request.OperationName);
            return StatusCode(500, new { error = "Benchmark failed", message = ex.Message });
        }
    }

    /// <summary>
    /// Runs multiple performance benchmarks
    /// </summary>
    [HttpPost("run-multiple")]
    public async Task<IActionResult> RunMultipleBenchmarks([FromBody] MultipleBenchmarkRequest request)
    {
        var results = new List<PerformanceMetrics>();

        foreach (var operation in request.Operations)
        {
            try
            {
                var metrics = await PerformanceBenchmarks.MeasureDetailedAsync(
                    operation.OperationName,
                    async () => await SimulateOperation(operation.OperationType, operation.DurationMs),
                    _logger);

                results.Add(metrics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Benchmark failed for operation: {OperationName}", operation.OperationName);
                results.Add(new PerformanceMetrics
                {
                    OperationName = operation.OperationName,
                    Success = false,
                    Error = ex.Message,
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        return Ok(results);
    }

    /// <summary>
    /// Gets performance statistics
    /// </summary>
    [HttpGet("stats")]
    public IActionResult GetPerformanceStats()
    {
        var process = Process.GetCurrentProcess();
        var stats = new
        {
            ProcessId = process.Id,
            ProcessName = process.ProcessName,
            WorkingSet = process.WorkingSet64,
            PrivateMemorySize = process.PrivateMemorySize64,
            VirtualMemorySize = process.VirtualMemorySize64,
            ThreadCount = process.Threads.Count,
            HandleCount = process.HandleCount,
            StartTime = process.StartTime,
            TotalProcessorTime = process.TotalProcessorTime.TotalMilliseconds,
            GcMemory = GC.GetTotalMemory(false),
            GcGen0Collections = GC.CollectionCount(0),
            GcGen1Collections = GC.CollectionCount(1),
            GcGen2Collections = GC.CollectionCount(2)
        };

        return Ok(stats);
    }

    private async Task<object> SimulateOperation(string operationType, int durationMs)
    {
        await Task.Delay(durationMs);
        
        return new
        {
            OperationType = operationType,
            DurationMs = durationMs,
            Timestamp = DateTime.UtcNow
        };
    }
}

/// <summary>
/// Benchmark request model
/// </summary>
public class BenchmarkRequest
{
    public string OperationName { get; set; } = "";
    public string OperationType { get; set; } = "";
    public int DurationMs { get; set; } = 100;
}

/// <summary>
/// Multiple benchmark request model
/// </summary>
public class MultipleBenchmarkRequest
{
    public List<BenchmarkOperation> Operations { get; set; } = new();
}

/// <summary>
/// Benchmark operation model
/// </summary>
public class BenchmarkOperation
{
    public string OperationName { get; set; } = "";
    public string OperationType { get; set; } = "";
    public int DurationMs { get; set; } = 100;
}

/// <summary>
/// Performance benchmarking extensions
/// </summary>
public static class PerformanceBenchmarkingExtensions
{
    /// <summary>
    /// Adds performance benchmarking to the service collection
    /// </summary>
    public static IServiceCollection AddPerformanceBenchmarking(this IServiceCollection services, IConfiguration configuration)
    {
        var config = configuration.GetSection("PerformanceBenchmarking").Get<PerformanceBenchmarkingConfiguration>() ?? new PerformanceBenchmarkingConfiguration();
        services.AddSingleton(config);
        return services;
    }

    /// <summary>
    /// Uses performance benchmarking in the application
    /// </summary>
    public static WebApplication UsePerformanceBenchmarking(this WebApplication app)
    {
        var config = app.Services.GetRequiredService<PerformanceBenchmarkingConfiguration>();
        if (config.Enabled)
        {
            app.UseMiddleware<PerformanceBenchmarkingMiddleware>();
        }
        return app;
    }
}
