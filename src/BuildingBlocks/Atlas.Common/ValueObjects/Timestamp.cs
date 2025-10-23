using System.Text.Json;
using System.Text.Json.Serialization;

namespace Atlas.Common.ValueObjects;

/// <summary>
/// Represents a timestamp with timezone information
/// </summary>
[JsonConverter(typeof(TimestampJsonConverter))]
public readonly record struct Timestamp
{
    public DateTimeOffset Value { get; }

    public Timestamp(DateTimeOffset value)
    {
        Value = value;
    }

    public static Timestamp Now => new(DateTimeOffset.UtcNow);
    public static Timestamp FromDateTimeOffset(DateTimeOffset value) => new(value);
    public static Timestamp FromUnixTimeSeconds(long seconds) => new(DateTimeOffset.FromUnixTimeSeconds(seconds));
    
    public static implicit operator DateTimeOffset(Timestamp timestamp) => timestamp.Value;
    public static implicit operator Timestamp(DateTimeOffset value) => new(value);

    public long ToUnixTimeSeconds() => Value.ToUnixTimeSeconds();
    public long ToUnixTimeMilliseconds() => Value.ToUnixTimeMilliseconds();

    public override string ToString() => Value.ToString("O");
}

public class TimestampJsonConverter : JsonConverter<Timestamp>
{
    public override Timestamp Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        if (value == null)
            throw new JsonException();
        
        return new Timestamp(DateTimeOffset.Parse(value));
    }

    public override void Write(Utf8JsonWriter writer, Timestamp value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value.ToString("O"));
    }
}
