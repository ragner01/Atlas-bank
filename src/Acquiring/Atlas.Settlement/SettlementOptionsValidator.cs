using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;

namespace Atlas.Settlement;

/// <summary>
/// Validator for SettlementOptions
/// </summary>
public class SettlementOptionsValidator : IValidateOptions<SettlementOptions>
{
    public ValidateOptionsResult Validate(string? name, SettlementOptions options)
    {
        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(options);

        if (!Validator.TryValidateObject(options, validationContext, validationResults, true))
        {
            var errors = string.Join(", ", validationResults.Select(r => r.ErrorMessage));
            return ValidateOptionsResult.Fail($"Configuration validation failed: {errors}");
        }

        return ValidateOptionsResult.Success;
    }
}
