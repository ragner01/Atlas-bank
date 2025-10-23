using System.Text.Json;
using System.Text.Json.Serialization;

namespace Atlas.Common.Results;

/// <summary>
/// Represents the result of an operation that can either succeed or fail
/// </summary>
[JsonConverter(typeof(ResultJsonConverter))]
public readonly record struct Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public string? Error { get; }
    public string? ErrorCode { get; }

    private Result(bool isSuccess, T? value, string? error, string? errorCode)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
        ErrorCode = errorCode;
    }

    public static Result<T> Success(T value) => new(true, value, null, null);
    public static Result<T> Failure(string error, string? errorCode = null) => new(false, default, error, errorCode);

    public Result<TNew> Map<TNew>(Func<T, TNew> mapper)
    {
        return IsSuccess ? Result<TNew>.Success(mapper(Value!)) : Result<TNew>.Failure(Error!, ErrorCode);
    }

    public Result<TNew> Bind<TNew>(Func<T, Result<TNew>> binder)
    {
        return IsSuccess ? binder(Value!) : Result<TNew>.Failure(Error!, ErrorCode);
    }

    public T GetValueOrThrow()
    {
        if (!IsSuccess)
            throw new InvalidOperationException($"Result is not successful: {Error}");
        return Value!;
    }

    public T GetValueOrDefault(T defaultValue = default!)
    {
        return IsSuccess ? Value! : defaultValue;
    }
}

public class ResultJsonConverter : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        return typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(Result<>);
    }

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var valueType = typeToConvert.GetGenericArguments()[0];
        var converterType = typeof(ResultJsonConverter<>).MakeGenericType(valueType);
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }
}

public class ResultJsonConverter<T> : JsonConverter<Result<T>>
{
    public override Result<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException();

        bool isSuccess = false;
        T? value = default;
        string? error = null;
        string? errorCode = null;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                break;

            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException();

            var propertyName = reader.GetString();
            reader.Read();

            switch (propertyName)
            {
                case "isSuccess":
                    isSuccess = reader.GetBoolean();
                    break;
                case "value":
                    if (isSuccess)
                        value = JsonSerializer.Deserialize<T>(ref reader, options);
                    break;
                case "error":
                    error = reader.GetString();
                    break;
                case "errorCode":
                    errorCode = reader.GetString();
                    break;
            }
        }

        return isSuccess ? Result<T>.Success(value!) : Result<T>.Failure(error ?? "Unknown error", errorCode);
    }

    public override void Write(Utf8JsonWriter writer, Result<T> value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteBoolean("isSuccess", value.IsSuccess);
        
        if (value.IsSuccess && value.Value != null)
        {
            writer.WritePropertyName("value");
            JsonSerializer.Serialize(writer, value.Value, options);
        }
        else if (!value.IsSuccess)
        {
            writer.WriteString("error", value.Error);
            if (!string.IsNullOrEmpty(value.ErrorCode))
                writer.WriteString("errorCode", value.ErrorCode);
        }
        
        writer.WriteEndObject();
    }
}