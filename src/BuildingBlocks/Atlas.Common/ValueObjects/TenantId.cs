using System.Text.Json;
using System.Text.Json.Serialization;

namespace Atlas.Common.ValueObjects;

/// <summary>
/// Represents a tenant identifier in a multi-tenant system
/// </summary>
[JsonConverter(typeof(TenantIdJsonConverter))]
public readonly record struct TenantId
{
    public string Value { get; }

    public TenantId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Tenant ID cannot be null or empty", nameof(value));
        
        if (value.Length > 50)
            throw new ArgumentException("Tenant ID cannot exceed 50 characters", nameof(value));
        
        Value = value.ToLowerInvariant();
    }

    public static TenantId FromString(string value) => new(value);
    
    public static implicit operator string(TenantId tenantId) => tenantId.Value;
    public static implicit operator TenantId(string value) => new(value);

    public override string ToString() => Value;
}

public class TenantIdJsonConverter : JsonConverter<TenantId>
{
    public override TenantId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return new TenantId(value ?? throw new JsonException());
    }

    public override void Write(Utf8JsonWriter writer, TenantId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}
