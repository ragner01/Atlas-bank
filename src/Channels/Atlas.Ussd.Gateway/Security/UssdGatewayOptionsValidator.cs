using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;

namespace Atlas.Ussd.Gateway.Security;

/// <summary>
/// Validator for UssdGatewayOptions
/// </summary>
public class UssdGatewayOptionsValidator : IValidateOptions<UssdGatewayOptions>
{
    public ValidateOptionsResult Validate(string? name, UssdGatewayOptions options)
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

