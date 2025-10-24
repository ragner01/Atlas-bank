using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace AtlasBank.BuildingBlocks.Documentation;

/// <summary>
/// Comprehensive API documentation configuration
/// </summary>
public static class ApiDocumentationConfiguration
{
    public static void AddAtlasApiDocumentation(this IServiceCollection services, IConfiguration configuration)
    {
        var docConfig = configuration.GetSection("ApiDocumentation").Get<ApiDocumentationConfiguration>() ?? new ApiDocumentationConfiguration();
        
        services.AddSwaggerGen(options =>
        {
            // Basic API information
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = docConfig.Title,
                Version = docConfig.Version,
                Description = docConfig.Description,
                Contact = new OpenApiContact
                {
                    Name = docConfig.ContactName,
                    Email = docConfig.ContactEmail,
                    Url = new Uri(docConfig.ContactUrl)
                },
                License = new OpenApiLicense
                {
                    Name = docConfig.LicenseName,
                    Url = new Uri(docConfig.LicenseUrl)
                },
                TermsOfService = new Uri(docConfig.TermsOfServiceUrl)
            });

            // Security definitions
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "JWT Authorization header using the Bearer scheme"
            });

            options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.ApiKey,
                In = ParameterLocation.Header,
                Name = "X-API-Key",
                Description = "API Key authentication"
            });

            // Security requirements
            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                },
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "ApiKey"
                        }
                    },
                    Array.Empty<string>()
                }
            });

            // Operation filters
            options.OperationFilter<ApiDocumentationOperationFilter>();
            options.OperationFilter<ApiVersionOperationFilter>();
            options.OperationFilter<ApiResponseOperationFilter>();
            options.OperationFilter<ApiParameterOperationFilter>();

            // Schema filters
            options.SchemaFilter<ApiDocumentationSchemaFilter>();
            options.SchemaFilter<ApiVersionSchemaFilter>();

            // Include XML comments if available
            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath);
            }

            // Custom document filter
            options.DocumentFilter<ApiDocumentationDocumentFilter>();
        });
    }

    public static void UseAtlasApiDocumentation(this WebApplication app)
    {
        app.UseSwagger(options =>
        {
            options.RouteTemplate = "swagger/{documentName}/swagger.json";
        });

        app.UseSwaggerUI(options =>
        {
            options.RoutePrefix = "swagger";
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "AtlasBank API v1");
            options.DocumentTitle = "AtlasBank API Documentation";
            options.DefaultModelsExpandDepth(-1);
            options.DefaultModelRendering(Swashbuckle.AspNetCore.SwaggerUI.ModelRendering.Model);
            options.DisplayRequestDuration();
            options.EnableDeepLinking();
            options.EnableFilter();
            options.ShowExtensions();
            options.EnableValidator();
        });
    }
}

/// <summary>
/// API documentation configuration model
/// </summary>
public class ApiDocumentationConfiguration
{
    public string Title { get; set; } = "AtlasBank API";
    public string Version { get; set; } = "v1";
    public string Description { get; set; } = "AtlasBank Financial Services API";
    public string ContactName { get; set; } = "AtlasBank Support";
    public string ContactEmail { get; set; } = "support@atlasbank.com";
    public string ContactUrl { get; set; } = "https://atlasbank.com/contact";
    public string LicenseName { get; set; } = "MIT";
    public string LicenseUrl { get; set; } = "https://opensource.org/licenses/MIT";
    public string TermsOfServiceUrl { get; set; } = "https://atlasbank.com/terms";
}

/// <summary>
/// API documentation operation filter
/// </summary>
public class ApiDocumentationOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        // Add operation ID
        operation.OperationId = context.MethodInfo.Name;

        // Add tags
        var controllerName = context.MethodInfo.DeclaringType?.Name.Replace("Controller", "");
        if (!string.IsNullOrEmpty(controllerName))
        {
            operation.Tags = new List<OpenApiTag> { new() { Name = controllerName } };
        }

        // Add summary and description
        var summary = context.MethodInfo.GetCustomAttribute<ApiDocumentationAttribute>()?.Summary;
        if (!string.IsNullOrEmpty(summary))
        {
            operation.Summary = summary;
        }

        var description = context.MethodInfo.GetCustomAttribute<ApiDocumentationAttribute>()?.Description;
        if (!string.IsNullOrEmpty(description))
        {
            operation.Description = description;
        }

        // Add examples
        var examples = context.MethodInfo.GetCustomAttributes<ApiExampleAttribute>();
        if (examples.Any())
        {
            operation.RequestBody = new OpenApiRequestBody
            {
                Content = new Dictionary<string, OpenApiMediaType>()
            };

            foreach (var example in examples)
            {
                operation.RequestBody.Content["application/json"] = new OpenApiMediaType
                {
                    Example = new OpenApiObject
                    {
                        [example.PropertyName] = new OpenApiString(example.ExampleValue)
                    }
                };
            }
        }
    }
}

/// <summary>
/// API response operation filter
/// </summary>
public class ApiResponseOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        // Add common response codes
        operation.Responses ??= new OpenApiResponses();

        if (!operation.Responses.ContainsKey("200"))
        {
            operation.Responses.Add("200", new OpenApiResponse
            {
                Description = "Success",
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = new OpenApiMediaType
                    {
                        Schema = new OpenApiSchema
                        {
                            Type = "object"
                        }
                    }
                }
            });
        }

        if (!operation.Responses.ContainsKey("400"))
        {
            operation.Responses.Add("400", new OpenApiResponse
            {
                Description = "Bad Request",
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = new OpenApiMediaType
                    {
                        Schema = new OpenApiSchema
                        {
                            Type = "object",
                            Properties = new Dictionary<string, OpenApiSchema>
                            {
                                ["error"] = new OpenApiSchema { Type = "string" },
                                ["message"] = new OpenApiSchema { Type = "string" },
                                ["details"] = new OpenApiSchema { Type = "string" }
                            }
                        }
                    }
                }
            });
        }

        if (!operation.Responses.ContainsKey("401"))
        {
            operation.Responses.Add("401", new OpenApiResponse
            {
                Description = "Unauthorized",
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = new OpenApiMediaType
                    {
                        Schema = new OpenApiSchema
                        {
                            Type = "object",
                            Properties = new Dictionary<string, OpenApiSchema>
                            {
                                ["error"] = new OpenApiSchema { Type = "string" },
                                ["message"] = new OpenApiSchema { Type = "string" }
                            }
                        }
                    }
                }
            });
        }

        if (!operation.Responses.ContainsKey("403"))
        {
            operation.Responses.Add("403", new OpenApiResponse
            {
                Description = "Forbidden",
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = new OpenApiMediaType
                    {
                        Schema = new OpenApiSchema
                        {
                            Type = "object",
                            Properties = new Dictionary<string, OpenApiSchema>
                            {
                                ["error"] = new OpenApiSchema { Type = "string" },
                                ["message"] = new OpenApiSchema { Type = "string" }
                            }
                        }
                    }
                }
            });
        }

        if (!operation.Responses.ContainsKey("404"))
        {
            operation.Responses.Add("404", new OpenApiResponse
            {
                Description = "Not Found",
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = new OpenApiMediaType
                    {
                        Schema = new OpenApiSchema
                        {
                            Type = "object",
                            Properties = new Dictionary<string, OpenApiSchema>
                            {
                                ["error"] = new OpenApiSchema { Type = "string" },
                                ["message"] = new OpenApiSchema { Type = "string" }
                            }
                        }
                    }
                }
            });
        }

        if (!operation.Responses.ContainsKey("500"))
        {
            operation.Responses.Add("500", new OpenApiResponse
            {
                Description = "Internal Server Error",
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = new OpenApiMediaType
                    {
                        Schema = new OpenApiSchema
                        {
                            Type = "object",
                            Properties = new Dictionary<string, OpenApiSchema>
                            {
                                ["error"] = new OpenApiSchema { Type = "string" },
                                ["message"] = new OpenApiSchema { Type = "string" },
                                ["correlationId"] = new OpenApiSchema { Type = "string" }
                            }
                        }
                    }
                }
            });
        }
    }
}

/// <summary>
/// API parameter operation filter
/// </summary>
public class ApiParameterOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        operation.Parameters ??= new List<OpenApiParameter>();

        // Add common headers
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "X-Correlation-ID",
            In = ParameterLocation.Header,
            Description = "Correlation ID for request tracking",
            Required = false,
            Schema = new OpenApiSchema
            {
                Type = "string",
                Format = "uuid"
            }
        });

        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "X-Tenant-ID",
            In = ParameterLocation.Header,
            Description = "Tenant ID for multi-tenancy",
            Required = false,
            Schema = new OpenApiSchema
            {
                Type = "string"
            }
        });

        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "X-Request-ID",
            In = ParameterLocation.Header,
            Description = "Unique request identifier",
            Required = false,
            Schema = new OpenApiSchema
            {
                Type = "string",
                Format = "uuid"
            }
        });
    }
}

/// <summary>
/// API documentation schema filter
/// </summary>
public class ApiDocumentationSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        // Add description from attributes
        var descriptionAttribute = context.Type.GetCustomAttribute<ApiDocumentationAttribute>();
        if (descriptionAttribute != null)
        {
            schema.Description = descriptionAttribute.Description;
        }

        // Add example from attributes
        var exampleAttribute = context.Type.GetCustomAttribute<ApiExampleAttribute>();
        if (exampleAttribute != null)
        {
            schema.Example = new OpenApiString(exampleAttribute.ExampleValue);
        }

        // Add validation information
        var validationAttributes = context.Type.GetCustomAttributes<ValidationAttribute>();
        foreach (var attribute in validationAttributes)
        {
            switch (attribute)
            {
                case RequiredAttribute required:
                    schema.Required ??= new HashSet<string>();
                    break;
                case StringLengthAttribute stringLength:
                    schema.MaxLength = stringLength.MaximumLength;
                    schema.MinLength = stringLength.MinimumLength;
                    break;
                case RangeAttribute range:
                    schema.Minimum = Convert.ToDecimal(range.Minimum);
                    schema.Maximum = Convert.ToDecimal(range.Maximum);
                    break;
            }
        }
    }
}

/// <summary>
/// API documentation document filter
/// </summary>
public class ApiDocumentationDocumentFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        // Add global tags
        swaggerDoc.Tags = new List<OpenApiTag>
        {
            new() { Name = "USSD", Description = "USSD Gateway operations" },
            new() { Name = "Agent", Description = "Agent Network operations" },
            new() { Name = "Offline", Description = "Offline Queue operations" },
            new() { Name = "Payments", Description = "Payment processing operations" },
            new() { Name = "Ledger", Description = "Ledger operations" },
            new() { Name = "Health", Description = "Health check operations" }
        };

        // Add global security schemes
        swaggerDoc.Components ??= new OpenApiComponents();
        swaggerDoc.Components.SecuritySchemes ??= new Dictionary<string, OpenApiSecurityScheme>();

        swaggerDoc.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "JWT Authorization header using the Bearer scheme"
        };

        swaggerDoc.Components.SecuritySchemes["ApiKey"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Header,
            Name = "X-API-Key",
            Description = "API Key authentication"
        };
    }
}

/// <summary>
/// API documentation attributes
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Property)]
public class ApiDocumentationAttribute : Attribute
{
    public string Summary { get; set; } = "";
    public string Description { get; set; } = "";
}

[AttributeUsage(AttributeTargets.Property)]
public class ApiExampleAttribute : Attribute
{
    public string PropertyName { get; set; } = "";
    public string ExampleValue { get; set; } = "";

    public ApiExampleAttribute(string propertyName, string exampleValue)
    {
        PropertyName = propertyName;
        ExampleValue = exampleValue;
    }
}

/// <summary>
/// API documentation extensions
/// </summary>
public static class ApiDocumentationExtensions
{
    /// <summary>
    /// Adds API documentation to the service collection
    /// </summary>
    public static IServiceCollection AddAtlasApiDocumentation(this IServiceCollection services, IConfiguration configuration)
    {
        ApiDocumentationConfiguration.AddAtlasApiDocumentation(services, configuration);
        return services;
    }

    /// <summary>
    /// Uses API documentation in the application
    /// </summary>
    public static WebApplication UseAtlasApiDocumentation(this WebApplication app)
    {
        ApiDocumentationConfiguration.UseAtlasApiDocumentation(app);
        return app;
    }
}
