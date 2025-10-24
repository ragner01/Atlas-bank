using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace Atlas.Ledger.Api.Configuration;

/// <summary>
/// Configuration options for the Ledger API
/// </summary>
public class LedgerApiOptions
{
    public const string SectionName = "LedgerApi";

    /// <summary>
    /// Maximum number of accounts that can be created per tenant
    /// </summary>
    [Range(1, 10000, ErrorMessage = "MaxAccountsPerTenant must be between 1 and 10000")]
    public int MaxAccountsPerTenant { get; set; } = 1000;

    /// <summary>
    /// Maximum transaction amount in minor units
    /// </summary>
    [Range(1, long.MaxValue, ErrorMessage = "MaxTransactionAmount must be greater than 0")]
    public long MaxTransactionAmount { get; set; } = 100000000; // 1M in minor units

    /// <summary>
    /// Enable fast transfer optimization
    /// </summary>
    public bool EnableFastTransfer { get; set; } = true;

    /// <summary>
    /// Connection timeout for database operations in seconds
    /// </summary>
    [Range(5, 300, ErrorMessage = "DatabaseTimeoutSeconds must be between 5 and 300")]
    public int DatabaseTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum retry count for database operations
    /// </summary>
    [Range(0, 10, ErrorMessage = "MaxRetryCount must be between 0 and 10")]
    public int MaxRetryCount { get; set; } = 3;
}

/// <summary>
/// Validator for LedgerApiOptions
/// </summary>
public class LedgerApiOptionsValidator : IValidateOptions<LedgerApiOptions>
{
    public ValidateOptionsResult Validate(string? name, LedgerApiOptions options)
    {
        var errors = new List<string>();

        if (options.MaxAccountsPerTenant <= 0)
            errors.Add("MaxAccountsPerTenant must be greater than 0");

        if (options.MaxTransactionAmount <= 0)
            errors.Add("MaxTransactionAmount must be greater than 0");

        if (options.DatabaseTimeoutSeconds <= 0)
            errors.Add("DatabaseTimeoutSeconds must be greater than 0");

        if (options.MaxRetryCount < 0)
            errors.Add("MaxRetryCount must be non-negative");

        return errors.Count > 0 
            ? ValidateOptionsResult.Fail(errors) 
            : ValidateOptionsResult.Success;
    }
}