using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace AtlasBank.Domain.Types;

/// <summary>
/// Strongly typed identifiers for better type safety
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EntityType
{
    Account,
    Transaction,
    Payment,
    User,
    Card,
    Loan,
    Agent
}

/// <summary>
/// Account ID with validation
/// </summary>
public record AccountId
{
    public string Value { get; }

    public AccountId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Account ID cannot be null or empty", nameof(value));
        
        if (value.Length > 50)
            throw new ArgumentException("Account ID cannot exceed 50 characters", nameof(value));

        Value = value;
    }

    public static implicit operator string(AccountId accountId) => accountId.Value;
    public static implicit operator AccountId(string value) => new(value);
    
    public override string ToString() => Value;
}

/// <summary>
/// Transaction ID with validation
/// </summary>
public record TransactionId
{
    public string Value { get; }

    public TransactionId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Transaction ID cannot be null or empty", nameof(value));
        
        if (value.Length > 50)
            throw new ArgumentException("Transaction ID cannot exceed 50 characters", nameof(value));

        Value = value;
    }

    public static implicit operator string(TransactionId transactionId) => transactionId.Value;
    public static implicit operator TransactionId(string value) => new(value);
    
    public override string ToString() => Value;
}

/// <summary>
/// User ID with validation
/// </summary>
public record UserId
{
    public string Value { get; }

    public UserId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("User ID cannot be null or empty", nameof(value));
        
        if (value.Length > 50)
            throw new ArgumentException("User ID cannot exceed 50 characters", nameof(value));

        Value = value;
    }

    public static implicit operator string(UserId userId) => userId.Value;
    public static implicit operator UserId(string value) => new(value);
    
    public override string ToString() => Value;
}

/// <summary>
/// MSISDN with validation
/// </summary>
public record Msisdn
{
    public string Value { get; }

    public Msisdn(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("MSISDN cannot be null or empty", nameof(value));
        
        // Basic MSISDN validation (should start with country code)
        if (!value.StartsWith("234") || value.Length < 10 || value.Length > 15)
            throw new ArgumentException("Invalid MSISDN format", nameof(value));

        Value = value;
    }

    public static implicit operator string(Msisdn msisdn) => msisdn.Value;
    public static implicit operator Msisdn(string value) => new(value);
    
    public override string ToString() => Value;
}

/// <summary>
/// PIN with validation
/// </summary>
public record Pin
{
    public string Value { get; }

    public Pin(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("PIN cannot be null or empty", nameof(value));
        
        if (value.Length < 4 || value.Length > 6)
            throw new ArgumentException("PIN must be between 4 and 6 digits", nameof(value));
        
        if (!value.All(char.IsDigit))
            throw new ArgumentException("PIN must contain only digits", nameof(value));

        Value = value;
    }

    public static implicit operator string(Pin pin) => pin.Value;
    public static implicit operator Pin(string value) => new(value);
    
    public override string ToString() => Value;
}

/// <summary>
/// Amount with validation
/// </summary>
public record Amount
{
    public decimal Value { get; }

    public Amount(decimal value)
    {
        if (value < 0)
            throw new ArgumentException("Amount cannot be negative", nameof(value));
        
        if (value > 999999999.99m)
            throw new ArgumentException("Amount cannot exceed 999,999,999.99", nameof(value));

        Value = value;
    }

    public static implicit operator decimal(Amount amount) => amount.Value;
    public static implicit operator Amount(decimal value) => new(value);
    
    public override string ToString() => Value.ToString("F2");
}

/// <summary>
/// Currency code with validation
/// </summary>
public record CurrencyCode
{
    public string Value { get; }

    public CurrencyCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Currency code cannot be null or empty", nameof(value));
        
        if (value.Length != 3)
            throw new ArgumentException("Currency code must be 3 characters", nameof(value));
        
        if (!value.All(char.IsLetter))
            throw new ArgumentException("Currency code must contain only letters", nameof(value));

        Value = value.ToUpperInvariant();
    }

    public static implicit operator string(CurrencyCode currencyCode) => currencyCode.Value;
    public static implicit operator CurrencyCode(string value) => new(value);
    
    public override string ToString() => Value;
}

/// <summary>
/// Email with validation
/// </summary>
public record Email
{
    public string Value { get; }

    public Email(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Email cannot be null or empty", nameof(value));
        
        if (!IsValidEmail(value))
            throw new ArgumentException("Invalid email format", nameof(value));

        Value = value.ToLowerInvariant();
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }

    public static implicit operator string(Email email) => email.Value;
    public static implicit operator Email(string value) => new(value);
    
    public override string ToString() => Value;
}

/// <summary>
/// Phone number with validation
/// </summary>
public record PhoneNumber
{
    public string Value { get; }

    public PhoneNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Phone number cannot be null or empty", nameof(value));
        
        // Remove all non-digit characters
        var digitsOnly = new string(value.Where(char.IsDigit).ToArray());
        
        if (digitsOnly.Length < 10 || digitsOnly.Length > 15)
            throw new ArgumentException("Phone number must be between 10 and 15 digits", nameof(value));

        Value = digitsOnly;
    }

    public static implicit operator string(PhoneNumber phoneNumber) => phoneNumber.Value;
    public static implicit operator PhoneNumber(string value) => new(value);
    
    public override string ToString() => Value;
}

/// <summary>
/// Result type for better error handling
/// </summary>
public class Result<T>
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public T Value { get; }
    public string Error { get; }

    private Result(bool isSuccess, T value, string error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    public static Result<T> Success(T value) => new(true, value, string.Empty);
    public static Result<T> Failure(string error) => new(false, default!, error);

    public static implicit operator Result<T>(T value) => Success(value);
    public static implicit operator Result<T>(string error) => Failure(error);
}

/// <summary>
/// Non-generic result type
/// </summary>
public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public string Error { get; }

    private Result(bool isSuccess, string error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public static Result Success() => new(true, string.Empty);
    public static Result Failure(string error) => new(false, error);

    public static implicit operator Result(string error) => Failure(error);
}

/// <summary>
/// Validation attributes for better type safety
/// </summary>
public class RequiredFieldAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        return value != null && !string.IsNullOrWhiteSpace(value.ToString());
    }

    public override string FormatErrorMessage(string name)
    {
        return $"{name} is required.";
    }
}

public class PositiveAmountAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        if (value is decimal decimalValue)
            return decimalValue > 0;
        
        if (value is Amount amount)
            return amount.Value > 0;
        
        return false;
    }

    public override string FormatErrorMessage(string name)
    {
        return $"{name} must be a positive amount.";
    }
}

public class ValidMsisdnAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        if (value is string stringValue)
        {
            try
            {
                var msisdn = new Msisdn(stringValue);
                return true;
            }
            catch
            {
                return false;
            }
        }
        
        return false;
    }

    public override string FormatErrorMessage(string name)
    {
        return $"{name} must be a valid MSISDN (e.g., 2348100000001).";
    }
}
