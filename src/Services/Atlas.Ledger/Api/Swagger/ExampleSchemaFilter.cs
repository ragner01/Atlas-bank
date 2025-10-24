using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Atlas.Ledger.Api.Models;
using Atlas.Ledger.App;

namespace Atlas.Ledger.Api.Swagger;

/// <summary>
/// Swagger schema filter to add examples from data annotations
/// </summary>
public class ExampleSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type == typeof(FastTransferRequest))
        {
            schema.Example = new Microsoft.OpenApi.Any.OpenApiObject
            {
                ["sourceAccountId"] = new Microsoft.OpenApi.Any.OpenApiString("acc_123456789"),
                ["destinationAccountId"] = new Microsoft.OpenApi.Any.OpenApiString("acc_987654321"),
                ["minor"] = new Microsoft.OpenApi.Any.OpenApiLong(10000),
                ["currency"] = new Microsoft.OpenApi.Any.OpenApiString("NGN"),
                ["narration"] = new Microsoft.OpenApi.Any.OpenApiString("Transfer between accounts"),
                ["tenantId"] = new Microsoft.OpenApi.Any.OpenApiString("tnt_demo")
            };
        }
        else if (context.Type == typeof(PostRequest))
        {
            schema.Example = new Microsoft.OpenApi.Any.OpenApiObject
            {
                ["sourceAccountId"] = new Microsoft.OpenApi.Any.OpenApiString("acc_123456789"),
                ["destinationAccountId"] = new Microsoft.OpenApi.Any.OpenApiString("acc_987654321"),
                ["minor"] = new Microsoft.OpenApi.Any.OpenApiLong(10000),
                ["currency"] = new Microsoft.OpenApi.Any.OpenApiString("NGN"),
                ["narration"] = new Microsoft.OpenApi.Any.OpenApiString("Journal entry transfer")
            };
        }
        else if (context.Type == typeof(ErrorResponse))
        {
            schema.Example = new Microsoft.OpenApi.Any.OpenApiObject
            {
                ["code"] = new Microsoft.OpenApi.Any.OpenApiString("VALIDATION_ERROR"),
                ["message"] = new Microsoft.OpenApi.Any.OpenApiString("Invalid request parameters"),
                ["details"] = new Microsoft.OpenApi.Any.OpenApiString("Source account ID is required"),
                ["correlationId"] = new Microsoft.OpenApi.Any.OpenApiString("12345678-1234-1234-1234-123456789012"),
                ["timestamp"] = new Microsoft.OpenApi.Any.OpenApiString("2024-01-15T10:30:00Z"),
                ["context"] = new Microsoft.OpenApi.Any.OpenApiObject
                {
                    ["validationErrors"] = new Microsoft.OpenApi.Any.OpenApiArray
                    {
                        new Microsoft.OpenApi.Any.OpenApiString("Source account ID is required"),
                        new Microsoft.OpenApi.Any.OpenApiString("Currency must be a 3-letter code")
                    }
                }
            };
        }
        else if (context.Type == typeof(SuccessResponse<object>))
        {
            schema.Example = new Microsoft.OpenApi.Any.OpenApiObject
            {
                ["data"] = new Microsoft.OpenApi.Any.OpenApiObject
                {
                    ["entryId"] = new Microsoft.OpenApi.Any.OpenApiString("12345678-1234-1234-1234-123456789012"),
                    ["status"] = new Microsoft.OpenApi.Any.OpenApiString("Pending")
                },
                ["message"] = new Microsoft.OpenApi.Any.OpenApiString("Success"),
                ["correlationId"] = new Microsoft.OpenApi.Any.OpenApiString("12345678-1234-1234-1234-123456789012"),
                ["timestamp"] = new Microsoft.OpenApi.Any.OpenApiString("2024-01-15T10:30:00Z")
            };
        }

        // Add descriptions from data annotations
        if (context.Type.GetProperties() is PropertyInfo[] properties)
        {
            foreach (var property in properties)
            {
                var displayAttribute = property.GetCustomAttribute<DisplayAttribute>();
                if (displayAttribute != null && !string.IsNullOrEmpty(displayAttribute.Description))
                {
                    var propertyName = char.ToLowerInvariant(property.Name[0]) + property.Name[1..];
                    if (schema.Properties.ContainsKey(propertyName))
                    {
                        schema.Properties[propertyName].Description = displayAttribute.Description;
                    }
                }
            }
        }
    }
}
