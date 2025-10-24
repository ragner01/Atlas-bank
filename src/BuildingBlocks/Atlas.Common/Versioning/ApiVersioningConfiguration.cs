using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Versioning;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;

namespace AtlasBank.Common.Versioning;

/// <summary>
/// API versioning configuration for AtlasBank services
/// </summary>
public static class ApiVersioningConfiguration
{
    /// <summary>
    /// Configure API versioning
    /// </summary>
    public static IServiceCollection AddApiVersioning(this IServiceCollection services)
    {
        services.AddApiVersioning(opt =>
        {
            opt.DefaultApiVersion = new ApiVersion(1, 0);
            opt.AssumeDefaultVersionWhenUnspecified = true;
            opt.ApiVersionReader = ApiVersionReader.Combine(
                new UrlSegmentApiVersionReader(),
                new HeaderApiVersionReader("X-Version"),
                new MediaTypeApiVersionReader("ver")
            );
            opt.ApiVersionSelector = new CurrentImplementationApiVersionSelector(opt);
        });

        services.AddVersionedApiExplorer(setup =>
        {
            setup.GroupNameFormat = "'v'VVV";
            setup.SubstituteApiVersionInUrl = true;
        });

        return services;
    }

    /// <summary>
    /// Configure Swagger with versioning
    /// </summary>
    public static IServiceCollection AddSwaggerWithVersioning(this IServiceCollection services)
    {
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "AtlasBank API",
                Version = "v1",
                Description = "AtlasBank Financial Services API",
                Contact = new OpenApiContact
                {
                    Name = "AtlasBank Team",
                    Email = "api@atlasbank.com"
                },
                License = new OpenApiLicense
                {
                    Name = "MIT License",
                    Url = new Uri("https://opensource.org/licenses/MIT")
                }
            });

            c.SwaggerDoc("v2", new OpenApiInfo
            {
                Title = "AtlasBank API",
                Version = "v2",
                Description = "AtlasBank Financial Services API v2 with enhanced features",
                Contact = new OpenApiContact
                {
                    Name = "AtlasBank Team",
                    Email = "api@atlasbank.com"
                }
            });

            // Add security definitions
            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.ApiKey,
                Scheme = "Bearer"
            });

            c.AddSecurityRequirement(new OpenApiSecurityRequirement
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
                }
            });

            // Include XML comments
            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
            {
                c.IncludeXmlComments(xmlPath);
            }

            c.OperationFilter<ApiVersionOperationFilter>();
            c.DocumentFilter<ApiVersionDocumentFilter>();
        });

        return services;
    }

    /// <summary>
    /// Configure Swagger UI with versioning
    /// </summary>
    public static IApplicationBuilder UseSwaggerWithVersioning(this IApplicationBuilder app, IApiVersionDescriptionProvider provider)
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            foreach (var description in provider.ApiVersionDescriptions)
            {
                c.SwaggerEndpoint(
                    $"/swagger/{description.GroupName}/swagger.json",
                    $"AtlasBank API {description.GroupName.ToUpperInvariant()}"
                );
            }

            c.RoutePrefix = "swagger";
            c.DocumentTitle = "AtlasBank API Documentation";
            c.DefaultModelsExpandDepth(-1);
            c.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.None);
        });

        return app;
    }
}

/// <summary>
/// Operation filter for API versioning
/// </summary>
public class ApiVersionOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var apiVersion = context.ApiDescription.GetApiVersion();
        if (apiVersion == null)
        {
            return;
        }

        operation.Parameters ??= new List<OpenApiParameter>();

        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "X-Version",
            In = ParameterLocation.Header,
            Required = false,
            Schema = new OpenApiSchema
            {
                Type = "string",
                Default = new OpenApiString(apiVersion.ToString())
            },
            Description = "API Version"
        });

        // Add version to operation summary
        operation.Summary = $"{operation.Summary} (v{apiVersion})";
    }
}

/// <summary>
/// Document filter for API versioning
/// </summary>
public class ApiVersionDocumentFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        var apiVersion = context.ApiDescription.GetApiVersion();
        if (apiVersion != null)
        {
            swaggerDoc.Info.Version = apiVersion.ToString();
            swaggerDoc.Info.Title = $"AtlasBank API v{apiVersion}";
        }
    }
}

/// <summary>
/// API version attributes
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class ApiVersionAttribute : ApiVersionAttribute
{
    public ApiVersionAttribute(int majorVersion, int minorVersion = 0) 
        : base(majorVersion, minorVersion)
    {
    }
}

/// <summary>
/// Deprecated API version attribute
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class DeprecatedApiVersionAttribute : ApiVersionAttribute
{
    public DeprecatedApiVersionAttribute(int majorVersion, int minorVersion = 0) 
        : base(majorVersion, minorVersion)
    {
        Deprecated = true;
    }
}

/// <summary>
/// Version-specific controller base
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public abstract class VersionedControllerBase : ControllerBase
{
    /// <summary>
    /// Get current API version
    /// </summary>
    protected ApiVersion CurrentVersion => HttpContext.GetRequestedApiVersion() ?? new ApiVersion(1, 0);

    /// <summary>
    /// Create versioned response
    /// </summary>
    protected ActionResult<T> VersionedResult<T>(T data)
    {
        Response.Headers.Add("X-API-Version", CurrentVersion.ToString());
        return Ok(data);
    }

    /// <summary>
    /// Create versioned error response
    /// </summary>
    protected ActionResult VersionedError(string message, int statusCode = 400)
    {
        Response.Headers.Add("X-API-Version", CurrentVersion.ToString());
        return StatusCode(statusCode, new { error = message, version = CurrentVersion.ToString() });
    }
}

