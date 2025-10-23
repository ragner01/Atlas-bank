using System.Text.Json;
using System.Text.Json.Serialization;

namespace Atlas.Common.ValueObjects;

/// <summary>
/// Represents a unique identifier for entities
/// </summary>
[JsonConverter(typeof(EntityIdJsonConverter))]
public readonly record struct EntityId
{
    public string Value { get; }

    public EntityId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Entity ID cannot be null or empty", nameof(value));
        
        Value = value;
    }

    public static EntityId NewId() => new(Guid.NewGuid().ToString("N"));
    public static EntityId FromString(string value) => new(value);
    
    public static implicit operator string(EntityId entityId) => entityId.Value;
    public static implicit operator EntityId(string value) => new(value);

    public override string ToString() => Value;
}

public class EntityIdJsonConverter : JsonConverter<EntityId>
{
    public override EntityId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return new EntityId(value ?? throw new JsonException());
    }

    public override void Write(Utf8JsonWriter writer, EntityId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}
