using System.Text.Json;
using System.Text.Json.Serialization;

namespace Atlas.Common.ValueObjects;

/// <summary>
/// Represents a currency with ISO 4217 code
/// </summary>
[JsonConverter(typeof(CurrencyJsonConverter))]
public readonly record struct Currency
{
    public string Code { get; }
    public string Name { get; }
    public int DecimalPlaces { get; }

    private Currency(string code, string name, int decimalPlaces)
    {
        Code = code;
        Name = name;
        DecimalPlaces = decimalPlaces;
    }

    public static Currency NGN => new("NGN", "Nigerian Naira", 2);
    public static Currency USD => new("USD", "US Dollar", 2);
    public static Currency EUR => new("EUR", "Euro", 2);
    public static Currency GBP => new("GBP", "British Pound", 2);

    public static Currency FromCode(string code) => code.ToUpperInvariant() switch
    {
        "NGN" => NGN,
        "USD" => USD,
        "EUR" => EUR,
        "GBP" => GBP,
        _ => throw new ArgumentException($"Unsupported currency code: {code}")
    };

    public override string ToString() => Code;
}

public class CurrencyJsonConverter : JsonConverter<Currency>
{
    public override Currency Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var code = reader.GetString();
        return Currency.FromCode(code ?? "NGN");
    }

    public override void Write(Utf8JsonWriter writer, Currency value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Code);
    }
}
