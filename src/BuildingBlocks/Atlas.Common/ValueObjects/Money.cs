using System.Text.Json;
using System.Text.Json.Serialization;

namespace Atlas.Common.ValueObjects;

/// <summary>
/// Represents a monetary amount with currency and scale
/// </summary>
[JsonConverter(typeof(MoneyJsonConverter))]
public readonly record struct Money
{
    public decimal Value { get; }
    public Currency Currency { get; }
    public int Scale { get; }

    public Money(decimal value, Currency currency, int scale = 2)
    {
        if (scale < 0 || scale > 8)
            throw new ArgumentOutOfRangeException(nameof(scale), "Scale must be between 0 and 8");
        
        Value = Math.Round(value, scale);
        Currency = currency;
        Scale = scale;
    }

    public static Money Zero(Currency currency, int scale = 2) => new(0, currency, scale);
    
    public static Money operator +(Money left, Money right)
    {
        if (left.Currency != right.Currency)
            throw new InvalidOperationException("Cannot add money with different currencies");
        
        var maxScale = Math.Max(left.Scale, right.Scale);
        return new Money(left.Value + right.Value, left.Currency, maxScale);
    }

    public static Money operator -(Money left, Money right)
    {
        if (left.Currency != right.Currency)
            throw new InvalidOperationException("Cannot subtract money with different currencies");
        
        var maxScale = Math.Max(left.Scale, right.Scale);
        return new Money(left.Value - right.Value, left.Currency, maxScale);
    }

    public static Money operator *(Money money, decimal multiplier) =>
        new(money.Value * multiplier, money.Currency, money.Scale);

    public static Money operator /(Money money, decimal divisor) =>
        new(money.Value / divisor, money.Currency, money.Scale);

    public static bool operator >(Money left, Money right)
    {
        if (left.Currency != right.Currency)
            throw new InvalidOperationException("Cannot compare money with different currencies");
        return left.Value > right.Value;
    }

    public static bool operator <(Money left, Money right)
    {
        if (left.Currency != right.Currency)
            throw new InvalidOperationException("Cannot compare money with different currencies");
        return left.Value < right.Value;
    }

    public static bool operator >=(Money left, Money right)
    {
        if (left.Currency != right.Currency)
            throw new InvalidOperationException("Cannot compare money with different currencies");
        return left.Value >= right.Value;
    }

    public static bool operator <=(Money left, Money right)
    {
        if (left.Currency != right.Currency)
            throw new InvalidOperationException("Cannot compare money with different currencies");
        return left.Value <= right.Value;
    }

    public bool IsZero => Value == 0;
    public bool IsPositive => Value > 0;
    public bool IsNegative => Value < 0;

    /// <summary>
    /// Gets the value in the lowest denomination (cents for most currencies)
    /// </summary>
    public long ValueInLowestDenomination => (long)(Value * (decimal)Math.Pow(10, Scale));

    /// <summary>
    /// Gets the value in ledger cents (for financial calculations)
    /// </summary>
    public long LedgerCents => ValueInLowestDenomination;

    public Money Abs() => new(Math.Abs(Value), Currency, Scale);
    public Money Negate() => new(-Value, Currency, Scale);

    public override string ToString() => string.Format("{0:F{1}} {2}", Value, Scale, Currency.Code);
}

public class MoneyJsonConverter : JsonConverter<Money>
{
    public override Money Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException();

        decimal value = 0;
        Currency currency = Currency.NGN;
        int scale = 2;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                return new Money(value, currency, scale);

            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException();

            var propertyName = reader.GetString();
            reader.Read();

            switch (propertyName)
            {
                case "value":
                    value = reader.GetDecimal();
                    break;
                case "currency":
                    currency = Currency.FromCode(reader.GetString() ?? "NGN");
                    break;
                case "scale":
                    scale = reader.GetInt32();
                    break;
            }
        }

        throw new JsonException();
    }

    public override void Write(Utf8JsonWriter writer, Money value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("value", value.Value);
        writer.WriteString("currency", value.Currency.Code);
        writer.WriteNumber("scale", value.Scale);
        writer.WriteEndObject();
    }
}
