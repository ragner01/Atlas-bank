using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using Xunit;
using Atlas.ApiGateway.Middleware;

namespace Atlas.ApiGateway.Tests.Middleware;

/// <summary>
/// Unit tests for GlobalExceptionHandlingMiddleware
/// </summary>
public class GlobalExceptionHandlingMiddlewareTests
{
    private readonly Mock<RequestDelegate> _mockNext;
    private readonly Mock<ILogger<GlobalExceptionHandlingMiddleware>> _mockLogger;
    private readonly GlobalExceptionHandlingMiddleware _middleware;

    public GlobalExceptionHandlingMiddlewareTests()
    {
        _mockNext = new Mock<RequestDelegate>();
        _mockLogger = new Mock<ILogger<GlobalExceptionHandlingMiddleware>>();
        _middleware = new GlobalExceptionHandlingMiddleware(_mockNext.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task InvokeAsync_NoException_CallsNext()
    {
        // Arrange
        var context = new DefaultHttpContext();
        _mockNext.Setup(x => x(context)).Returns(Task.CompletedTask);

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        _mockNext.Verify(x => x(context), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_UnauthorizedAccessException_Returns401()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        _mockNext.Setup(x => x(context)).ThrowsAsync(new UnauthorizedAccessException("Test exception"));

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(401, context.Response.StatusCode);
        Assert.Equal("application/json", context.Response.ContentType);

        var responseBody = await GetResponseBody(context.Response.Body);
        var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(responseBody);
        
        Assert.Equal("UNAUTHORIZED", errorResponse.Error.Code);
        Assert.Equal("Authentication required", errorResponse.Error.Message);
    }

    [Fact]
    public async Task InvokeAsync_ArgumentException_Returns400()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        _mockNext.Setup(x => x(context)).ThrowsAsync(new ArgumentException("Test exception"));

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(400, context.Response.StatusCode);
        Assert.Equal("application/json", context.Response.ContentType);

        var responseBody = await GetResponseBody(context.Response.Body);
        var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(responseBody);
        
        Assert.Equal("INVALID_ARGUMENT", errorResponse.Error.Code);
        Assert.Equal("Invalid request parameters", errorResponse.Error.Message);
    }

    [Fact]
    public async Task InvokeAsync_TimeoutException_Returns504()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        _mockNext.Setup(x => x(context)).ThrowsAsync(new TimeoutException("Test exception"));

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(504, context.Response.StatusCode);
        Assert.Equal("application/json", context.Response.ContentType);

        var responseBody = await GetResponseBody(context.Response.Body);
        var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(responseBody);
        
        Assert.Equal("TIMEOUT", errorResponse.Error.Code);
        Assert.Equal("Request timeout", errorResponse.Error.Message);
    }

    [Fact]
    public async Task InvokeAsync_GenericException_Returns500()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        _mockNext.Setup(x => x(context)).ThrowsAsync(new Exception("Test exception"));

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(500, context.Response.StatusCode);
        Assert.Equal("application/json", context.Response.ContentType);

        var responseBody = await GetResponseBody(context.Response.Body);
        var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(responseBody);
        
        Assert.Equal("INTERNAL_ERROR", errorResponse.Error.Code);
        Assert.Equal("An internal error occurred", errorResponse.Error.Message);
    }

    [Fact]
    public async Task InvokeAsync_IncludesRequestId()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.TraceIdentifier = "test-request-id";
        _mockNext.Setup(x => x(context)).ThrowsAsync(new Exception("Test exception"));

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        var responseBody = await GetResponseBody(context.Response.Body);
        var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(responseBody);
        
        Assert.Equal("test-request-id", errorResponse.Error.RequestId);
    }

    private static async Task<string> GetResponseBody(Stream body)
    {
        body.Position = 0;
        using var reader = new StreamReader(body);
        return await reader.ReadToEndAsync();
    }

    private record ErrorResponse(ErrorInfo Error);
    private record ErrorInfo(string Code, string Message, string RequestId, DateTimeOffset Timestamp);
}
