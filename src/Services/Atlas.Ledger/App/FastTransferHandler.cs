using Npgsql;
using System.Data;
using Atlas.Common.ValueObjects;
using System.Text.RegularExpressions;

namespace Atlas.Ledger.App;

/// <summary>
/// High-performance transfer handler with comprehensive input validation
/// </summary>
public sealed class FastTransferHandler
{
    private readonly NpgsqlDataSource _ds;
    private readonly ILogger<FastTransferHandler> _logger;
    
    // Validation patterns
    private static readonly Regex AccountIdPattern = new(@"^[a-zA-Z0-9_-]{1,50}$", RegexOptions.Compiled);
    private static readonly Regex TenantIdPattern = new(@"^tnt_[a-zA-Z0-9_-]{4,46}$", RegexOptions.Compiled);
    private static readonly Regex CurrencyPattern = new(@"^[A-Z]{3}$", RegexOptions.Compiled);
    private static readonly Regex NarrationPattern = new(@"^[a-zA-Z0-9\s\-_.,]{1,256}$", RegexOptions.Compiled);
    
    // Supported currencies
    private static readonly HashSet<string> SupportedCurrencies = new() { "NGN", "USD", "EUR", "GBP" };
    
    // Configuration constants
    private const int MaxRetries = 3;
    private const long MinAmountMinor = 1;
    private const long MaxAmountMinor = 1_000_000_000; // 10M in minor units

    public FastTransferHandler(NpgsqlDataSource ds, ILogger<FastTransferHandler> logger)
    {
        _ds = ds;
        _logger = logger;
    }

    /// <summary>
    /// Executes a fast transfer with comprehensive validation and retry logic
    /// </summary>
    public async Task<(Guid? entryId, bool duplicate)> ExecuteAsync(
        string key, string tenant, string src, string dst, long amountMinor, string currency, string narration,
        CancellationToken ct)
    {
        var correlationId = Guid.NewGuid().ToString();
        _logger.LogInformation("Starting fast transfer with correlation ID: {CorrelationId}", correlationId);

        // Comprehensive input validation
        var validationResult = ValidateInputs(key, tenant, src, dst, amountMinor, currency, narration);
        if (!validationResult.IsValid)
        {
            _logger.LogWarning("Input validation failed for correlation ID {CorrelationId}: {Errors}", 
                correlationId, string.Join(", ", validationResult.Errors));
            throw new ArgumentException($"Input validation failed: {string.Join(", ", validationResult.Errors)}");
        }

        _logger.LogDebug("Input validation passed for correlation ID: {CorrelationId}", correlationId);

        // Execute with retry logic
        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                _logger.LogDebug("Attempt {Attempt}/{MaxRetries} for correlation ID: {CorrelationId}", 
                    attempt, MaxRetries, correlationId);

                await using var conn = await _ds.OpenConnectionAsync(ct);
                await using var tx = await conn.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                
                await using var cmd = new NpgsqlCommand("SELECT sp_idem_transfer_execute(@k,@t,@s,@d,@m,@c,@n)", conn, tx);
                cmd.Parameters.AddWithValue("k", key);
                cmd.Parameters.AddWithValue("t", tenant);
                cmd.Parameters.AddWithValue("s", src);
                cmd.Parameters.AddWithValue("d", dst);
                cmd.Parameters.AddWithValue("m", amountMinor);
                cmd.Parameters.AddWithValue("c", currency);
                cmd.Parameters.AddWithValue("n", narration);
                
                var result = await cmd.ExecuteScalarAsync(ct);
                await tx.CommitAsync(ct);
                
                var entryId = result as Guid?;
                var isDuplicate = result is null;
                
                _logger.LogInformation("Fast transfer completed for correlation ID {CorrelationId}: EntryId={EntryId}, Duplicate={Duplicate}", 
                    correlationId, entryId, isDuplicate);
                
                return (entryId, isDuplicate);
            }
            catch (PostgresException ex) when (ex.SqlState == "40001") // serialization_failure
            {
                _logger.LogWarning("Serialization failure on attempt {Attempt}/{MaxRetries} for correlation ID {CorrelationId}", 
                    attempt, MaxRetries, correlationId);
                
                if (attempt == MaxRetries)
                {
                    _logger.LogError("Max retries exceeded for correlation ID {CorrelationId}", correlationId);
                    throw new InvalidOperationException($"Transfer failed after {MaxRetries} attempts due to serialization conflicts");
                }
                
                // Exponential backoff
                await Task.Delay(TimeSpan.FromMilliseconds(100 * attempt), ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during fast transfer for correlation ID {CorrelationId}", correlationId);
                throw;
            }
        }
        
        return (null, false);
    }

    /// <summary>
    /// Validates all input parameters comprehensively
    /// </summary>
    private static ValidationResult ValidateInputs(string key, string tenant, string src, string dst, 
        long amountMinor, string currency, string narration)
    {
        var errors = new List<string>();

        // Validate idempotency key
        if (string.IsNullOrWhiteSpace(key))
            errors.Add("Idempotency key is required");
        else if (key.Length > 100)
            errors.Add("Idempotency key must be 100 characters or less");

        // Validate tenant ID
        if (string.IsNullOrWhiteSpace(tenant))
            errors.Add("Tenant ID is required");
        else if (!TenantIdPattern.IsMatch(tenant))
            errors.Add("Tenant ID must start with 'tnt_' and contain only alphanumeric characters, underscores, and hyphens");

        // Validate source account ID
        if (string.IsNullOrWhiteSpace(src))
            errors.Add("Source account ID is required");
        else if (!AccountIdPattern.IsMatch(src))
            errors.Add("Source account ID must contain only alphanumeric characters, underscores, and hyphens (1-50 characters)");

        // Validate destination account ID
        if (string.IsNullOrWhiteSpace(dst))
            errors.Add("Destination account ID is required");
        else if (!AccountIdPattern.IsMatch(dst))
            errors.Add("Destination account ID must contain only alphanumeric characters, underscores, and hyphens (1-50 characters)");

        // Validate account IDs are different
        if (src.Equals(dst, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"Source and destination accounts must be different: src='{src}', dst='{dst}'");
        }

        // Validate amount
        if (amountMinor < MinAmountMinor)
            errors.Add($"Amount must be at least {MinAmountMinor} minor units");
        else if (amountMinor > MaxAmountMinor)
            errors.Add($"Amount must not exceed {MaxAmountMinor} minor units");

        // Validate currency
        if (string.IsNullOrWhiteSpace(currency))
            errors.Add("Currency is required");
        else if (!CurrencyPattern.IsMatch(currency))
            errors.Add("Currency must be a 3-letter uppercase code");
        else if (!SupportedCurrencies.Contains(currency))
            errors.Add($"Currency '{currency}' is not supported. Supported currencies: {string.Join(", ", SupportedCurrencies)}");

        // Validate narration
        if (string.IsNullOrWhiteSpace(narration))
            errors.Add("Narration is required");
        else if (!NarrationPattern.IsMatch(narration))
            errors.Add("Narration contains invalid characters or exceeds 256 characters");

        return new ValidationResult(errors.Count == 0, errors);
    }

    /// <summary>
    /// Represents the result of input validation
    /// </summary>
    private record ValidationResult(bool IsValid, List<string> Errors);
}
