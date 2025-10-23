using System.Text.Json;
using System.Text.Json.Serialization;

namespace Atlas.Common.Results;

/// <summary>
/// Represents the result of an operation that can either succeed or fail (without a value)
/// </summary>
[JsonConverter(typeof(ResultJsonConverter))]
public readonly record struct Result
{
    public bool IsSuccess { get; }
    public string? Error { get; }
    public string? ErrorCode { get; }

    private Result(bool isSuccess, string? error, string? errorCode)
    {
        IsSuccess = isSuccess;
        Error = error;
        ErrorCode = errorCode;
    }

    public static Result Success() => new(true, null, null);
    public static Result Failure(string error, string? errorCode = null) => new(false, error, errorCode);

    public Result<T> Map<T>(Func<T> mapper)
    {
        return IsSuccess ? Result<T>.Success(mapper()) : Result<T>.Failure(Error!, ErrorCode);
    }

    public Result Bind(Func<Result> binder)
    {
        return IsSuccess ? binder() : this;
    }

    public void ThrowIfFailure()
    {
        if (!IsSuccess)
            throw new InvalidOperationException($"Result is not successful: {Error}");
    }
}

public class ResultNonGenericJsonConverter : JsonConverter<Result>
{
    public override Result Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException();

        bool isSuccess = false;
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
                case "error":
                    error = reader.GetString();
                    break;
                case "errorCode":
                    errorCode = reader.GetString();
                    break;
            }
        }

        return isSuccess ? Result.Success() : Result.Failure(error ?? "Unknown error", errorCode);
    }

    public override void Write(Utf8JsonWriter writer, Result value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteBoolean("isSuccess", value.IsSuccess);
        
        if (!value.IsSuccess)
        {
            writer.WriteString("error", value.Error);
            if (!string.IsNullOrEmpty(value.ErrorCode))
                writer.WriteString("errorCode", value.ErrorCode);
        }
        
        writer.WriteEndObject();
    }
}
