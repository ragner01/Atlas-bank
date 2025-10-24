using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Versioning;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;

namespace AtlasBank.BuildingBlocks.Versioning;

/// <summary>
/// Comprehensive API versioning configuration
/// </summary>
public static class ApiVersioningConfiguration
{
    public static void AddAtlasApiVersioning(this IServiceCollection services)
    {
        services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ReportApiVersions = true;
            options.ApiVersionReader = ApiVersionReader.Combine(
                new UrlSegmentApiVersionReader(),
                new HeaderApiVersionReader("X-Version"),
                new MediaTypeApiVersionReader("ver")
            );
        });

        services.AddVersionedApiExplorer(options =>
        {
            options.GroupNameFormat = "'v'VVV";
            options.SubstituteApiVersionInUrl = true;
        });

        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "AtlasBank API",
                Version = "v1",
                Description = "AtlasBank Financial Services API",
                Contact = new OpenApiContact
                {
                    Name = "AtlasBank Support",
                    Email = "support@atlasbank.com"
                }
            });

            options.SwaggerDoc("v2", new OpenApiInfo
            {
                Title = "AtlasBank API",
                Version = "v2",
                Description = "AtlasBank Financial Services API v2",
                Contact = new OpenApiContact
                {
                    Name = "AtlasBank Support",
                    Email = "support@atlasbank.com"
                }
            });

            options.DocInclusionPredicate((version, desc) =>
            {
                if (!desc.TryGetMethodInfo(out MethodInfo methodInfo))
                    return false;

                var versions = methodInfo.DeclaringType
                    .GetCustomAttributes(true)
                    .OfType<ApiVersionAttribute>()
                    .SelectMany(attr => attr.Versions);

                return versions.Any(v => $"v{v}" == version);
            });

            options.OperationFilter<ApiVersionOperationFilter>();
            options.SchemaFilter<ApiVersionSchemaFilter>();
        });
    }

    public static void UseAtlasApiVersioning(this WebApplication app)
    {
        var apiVersionDescriptionProvider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();
        
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            foreach (var description in apiVersionDescriptionProvider.ApiVersionDescriptions)
            {
                options.SwaggerEndpoint($"/swagger/{description.GroupName}/swagger.json",
                    $"AtlasBank API {description.GroupName.ToUpperInvariant()}");
            }
        });
    }
}

/// <summary>
/// API version operation filter for Swagger
/// </summary>
public class ApiVersionOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var apiVersion = context.ApiDescription.GroupName;
        if (!string.IsNullOrEmpty(apiVersion))
        {
            operation.Parameters ??= new List<OpenApiParameter>();
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "X-Version",
                In = ParameterLocation.Header,
                Description = "API Version",
                Required = false,
                Schema = new OpenApiSchema
                {
                    Type = "string",
                    Default = new OpenApiString(apiVersion)
                }
            });
        }

        // Add common headers
        operation.Parameters ??= new List<OpenApiParameter>();
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
    }
}

/// <summary>
/// API version schema filter for Swagger
/// </summary>
public class ApiVersionSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type == typeof(ApiVersion))
        {
            schema.Type = "string";
            schema.Format = "version";
            schema.Example = new OpenApiString("1.0");
        }
    }
}

/// <summary>
/// API version attributes
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class ApiVersionAttribute : Attribute
{
    public ApiVersion[] Versions { get; }

    public ApiVersionAttribute(params string[] versions)
    {
        Versions = versions.Select(v => ApiVersion.Parse(v)).ToArray();
    }
}

/// <summary>
/// API version constants
/// </summary>
public static class ApiVersions
{
    public static readonly ApiVersion V1 = new(1, 0);
    public static readonly ApiVersion V2 = new(2, 0);
}

/// <summary>
/// API versioning middleware
/// </summary>
public class ApiVersioningMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiVersioningMiddleware> _logger;

    public ApiVersioningMiddleware(RequestDelegate next, ILogger<ApiVersioningMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";
        
        // Extract version from path
        var version = ExtractVersionFromPath(path);
        if (!string.IsNullOrEmpty(version))
        {
            context.Items["ApiVersion"] = version;
            _logger.LogDebug("API version {Version} detected from path {Path}", version, path);
        }

        // Extract version from header
        var headerVersion = context.Request.Headers["X-Version"].FirstOrDefault();
        if (!string.IsNullOrEmpty(headerVersion))
        {
            context.Items["ApiVersion"] = headerVersion;
            _logger.LogDebug("API version {Version} detected from header", headerVersion);
        }

        await _next(context);
    }

    private string? ExtractVersionFromPath(string path)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length > 0 && segments[0].StartsWith("v") && segments[0].Length > 1)
        {
            return segments[0];
        }
        return null;
    }
}

/// <summary>
/// API versioning utilities
/// </summary>
public static class ApiVersioningUtilities
{
    /// <summary>
    /// Gets the current API version from the request context
    /// </summary>
    public static string? GetCurrentApiVersion(HttpContext context)
    {
        return context.Items["ApiVersion"]?.ToString();
    }

    /// <summary>
    /// Checks if the current request is for a specific API version
    /// </summary>
    public static bool IsApiVersion(HttpContext context, string version)
    {
        var currentVersion = GetCurrentApiVersion(context);
        return string.Equals(currentVersion, version, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets the API version from the route data
    /// </summary>
    public static string? GetApiVersionFromRoute(HttpContext context)
    {
        if (context.Request.RouteValues.TryGetValue("version", out var version))
        {
            return version?.ToString();
        }
        return null;
    }

    /// <summary>
    /// Gets the API version from query parameters
    /// </summary>
    public static string? GetApiVersionFromQuery(HttpContext context)
    {
        return context.Request.Query["version"].FirstOrDefault();
    }

    /// <summary>
    /// Gets the API version from media type
    /// </summary>
    public static string? GetApiVersionFromMediaType(HttpContext context)
    {
        var contentType = context.Request.ContentType;
        if (string.IsNullOrEmpty(contentType))
            return null;

        var parts = contentType.Split(';');
        foreach (var part in parts)
        {
            if (part.Trim().StartsWith("ver=", StringComparison.OrdinalIgnoreCase))
            {
                return part.Trim().Substring(4);
            }
        }
        return null;
    }
}

/// <summary>
/// API versioning extensions
/// </summary>
public static class ApiVersioningExtensions
{
    /// <summary>
    /// Adds API versioning to the service collection
    /// </summary>
    public static IServiceCollection AddAtlasApiVersioning(this IServiceCollection services)
    {
        ApiVersioningConfiguration.AddAtlasApiVersioning(services);
        return services;
    }

    /// <summary>
    /// Uses API versioning in the application
    /// </summary>
    public static WebApplication UseAtlasApiVersioning(this WebApplication app)
    {
        ApiVersioningConfiguration.UseAtlasApiVersioning(app);
        return app;
    }
}
