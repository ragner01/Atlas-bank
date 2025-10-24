using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Atlas.Ledger.Api.Metrics;

/// <summary>
/// Application metrics collector for Atlas Bank Ledger API
/// </summary>
public class LedgerMetricsCollector : IHostedService
{
    private readonly Meter _meter;
    private readonly ILogger<LedgerMetricsCollector> _logger;
    private readonly IServiceProvider _serviceProvider;

    // Counters
    private readonly Counter<long> _transferRequestsTotal;
    private readonly Counter<long> _transferRequestsFailed;
    private readonly Counter<long> _journalEntriesCreated;
    private readonly Counter<long> _accountBalanceReads;
    private readonly Counter<long> _rateLimitHits;

    // Histograms
    private readonly Histogram<double> _transferDuration;
    private readonly Histogram<double> _balanceReadDuration;
    private readonly Histogram<double> _journalEntryDuration;

    // Gauges
    // private readonly ObservableGauge<long> _activeConnections;
    // private readonly ObservableGauge<long> _pendingTransactions;

    public LedgerMetricsCollector(IServiceProvider serviceProvider, ILogger<LedgerMetricsCollector> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _meter = new Meter("AtlasBank.Ledger", "1.0.0");

        // Initialize counters
        _transferRequestsTotal = _meter.CreateCounter<long>(
            "ledger_transfer_requests_total",
            "Total number of transfer requests");

        _transferRequestsFailed = _meter.CreateCounter<long>(
            "ledger_transfer_requests_failed_total",
            "Total number of failed transfer requests");

        _journalEntriesCreated = _meter.CreateCounter<long>(
            "ledger_journal_entries_created_total",
            "Total number of journal entries created");

        _accountBalanceReads = _meter.CreateCounter<long>(
            "ledger_account_balance_reads_total",
            "Total number of account balance reads");

        _rateLimitHits = _meter.CreateCounter<long>(
            "ledger_rate_limit_hits_total",
            "Total number of rate limit hits");

        // Initialize histograms
        _transferDuration = _meter.CreateHistogram<double>(
            "ledger_transfer_duration_seconds",
            "Duration of transfer operations in seconds");

        _balanceReadDuration = _meter.CreateHistogram<double>(
            "ledger_balance_read_duration_seconds",
            "Duration of balance read operations in seconds");

        _journalEntryDuration = _meter.CreateHistogram<double>(
            "ledger_journal_entry_duration_seconds",
            "Duration of journal entry operations in seconds");

        // TODO: Add gauges when ObservableGauge API is clarified
        // _activeConnections = _meter.CreateObservableGauge<long>(...);
        // _pendingTransactions = _meter.CreateObservableGauge<long>(...);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting Ledger metrics collection");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping Ledger metrics collection");
        _meter.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Records a transfer request
    /// </summary>
    public void RecordTransferRequest(string currency, string tenantId, bool success)
    {
        var status = success ? "success" : "failed";
        _transferRequestsTotal.Add(1, new KeyValuePair<string, object?>("currency", currency),
            new KeyValuePair<string, object?>("tenant_id", tenantId),
            new KeyValuePair<string, object?>("status", status));
    }

    /// <summary>
    /// Records a failed transfer request with error type
    /// </summary>
    public void RecordTransferFailure(string currency, string tenantId, string errorType)
    {
        _transferRequestsFailed.Add(1, new KeyValuePair<string, object?>("currency", currency),
            new KeyValuePair<string, object?>("tenant_id", tenantId),
            new KeyValuePair<string, object?>("error_type", errorType));
    }

    /// <summary>
    /// Records transfer duration
    /// </summary>
    public void RecordTransferDuration(double durationSeconds, string currency, string tenantId)
    {
        _transferDuration.Record(durationSeconds, new KeyValuePair<string, object?>("currency", currency),
            new KeyValuePair<string, object?>("tenant_id", tenantId));
    }

    /// <summary>
    /// Records a journal entry creation
    /// </summary>
    public void RecordJournalEntryCreated(string tenantId)
    {
        _journalEntriesCreated.Add(1, new KeyValuePair<string, object?>("tenant_id", tenantId));
    }

    /// <summary>
    /// Records journal entry duration
    /// </summary>
    public void RecordJournalEntryDuration(double durationSeconds, string tenantId)
    {
        _journalEntryDuration.Record(durationSeconds, new KeyValuePair<string, object?>("tenant_id", tenantId));
    }

    /// <summary>
    /// Records an account balance read
    /// </summary>
    public void RecordAccountBalanceRead(string tenantId, string source)
    {
        _accountBalanceReads.Add(1, new KeyValuePair<string, object?>("tenant_id", tenantId),
            new KeyValuePair<string, object?>("source", source));
    }

    /// <summary>
    /// Records balance read duration
    /// </summary>
    public void RecordBalanceReadDuration(double durationSeconds, string tenantId, string source)
    {
        _balanceReadDuration.Record(durationSeconds, new KeyValuePair<string, object?>("tenant_id", tenantId),
            new KeyValuePair<string, object?>("source", source));
    }

    /// <summary>
    /// Records a rate limit hit
    /// </summary>
    public void RecordRateLimitHit(string endpoint, string clientType)
    {
        _rateLimitHits.Add(1, new KeyValuePair<string, object?>("endpoint", endpoint),
            new KeyValuePair<string, object?>("client_type", clientType));
    }

    private long GetActiveConnectionsCount()
    {
        // TODO: Implement actual connection count retrieval
        return 0;
    }

    private long GetPendingTransactionsCount()
    {
        // TODO: Implement actual pending transaction count retrieval
        return 0;
    }
}

/// <summary>
/// Metrics middleware to automatically collect HTTP request metrics
/// </summary>
public class MetricsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly LedgerMetricsCollector _metricsCollector;
    private readonly ILogger<MetricsMiddleware> _logger;

    public MetricsMiddleware(RequestDelegate next, LedgerMetricsCollector metricsCollector, ILogger<MetricsMiddleware> logger)
    {
        _next = next;
        _metricsCollector = metricsCollector;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var correlationId = context.TraceIdentifier;

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            var duration = stopwatch.Elapsed.TotalSeconds;
            var tenantId = context.Request.Headers["X-Tenant-Id"].FirstOrDefault() ?? "unknown";
            var endpoint = GetEndpointName(context);

            // Record metrics based on endpoint
            switch (endpoint)
            {
                case "fast-transfer":
                    var currency = context.Request.Query["currency"].FirstOrDefault() ?? "unknown";
                    _metricsCollector.RecordTransferDuration(duration, currency, tenantId);
                    break;
                case "balance-read":
                    var source = context.Response.Headers["X-Source"].FirstOrDefault() ?? "unknown";
                    _metricsCollector.RecordBalanceReadDuration(duration, tenantId, source);
                    break;
                case "journal-entry":
                    _metricsCollector.RecordJournalEntryDuration(duration, tenantId);
                    break;
            }

            _logger.LogDebug("Request completed for endpoint {Endpoint} with duration {Duration}s and correlation ID {CorrelationId}",
                endpoint, duration, correlationId);
        }
    }

    private static string GetEndpointName(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";
        var method = context.Request.Method.ToUpperInvariant();

        return (method, path) switch
        {
            ("POST", "/ledger/fast-transfer") => "fast-transfer",
            ("GET", var p) when p.Contains("/ledger/accounts/") && p.Contains("/balance") => "balance-read",
            ("POST", "/ledger/entries") => "journal-entry",
            _ => "other"
        };
    }
}
