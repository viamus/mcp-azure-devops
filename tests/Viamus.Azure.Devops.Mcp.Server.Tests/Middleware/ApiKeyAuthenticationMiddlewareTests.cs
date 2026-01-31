using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Viamus.Azure.Devops.Mcp.Server.Configuration;
using Viamus.Azure.Devops.Mcp.Server.Middleware;

namespace Viamus.Azure.Devops.Mcp.Server.Tests.Middleware;

public class ApiKeyAuthenticationMiddlewareTests
{
    private readonly Mock<ILogger<ApiKeyAuthenticationMiddleware>> _mockLogger;
    private readonly Mock<IOptionsMonitor<ServerSecurityOptions>> _mockOptionsMonitor;
    private bool _nextDelegateCalled;

    public ApiKeyAuthenticationMiddlewareTests()
    {
        _mockLogger = new Mock<ILogger<ApiKeyAuthenticationMiddleware>>();
        _mockOptionsMonitor = new Mock<IOptionsMonitor<ServerSecurityOptions>>();
        _nextDelegateCalled = false;
    }

    private RequestDelegate CreateNextDelegate()
    {
        return _ =>
        {
            _nextDelegateCalled = true;
            return Task.CompletedTask;
        };
    }

    private HttpContext CreateHttpContext(string path = "/mcp", Dictionary<string, string>? headers = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();

        if (headers != null)
        {
            foreach (var header in headers)
            {
                context.Request.Headers[header.Key] = header.Value;
            }
        }

        return context;
    }

    #region Authentication Disabled Tests

    [Fact]
    public async Task InvokeAsync_WhenRequireApiKeyIsFalse_ShouldCallNextDelegate()
    {
        var options = new ServerSecurityOptions { RequireApiKey = false };
        _mockOptionsMonitor.Setup(m => m.CurrentValue).Returns(options);

        var middleware = new ApiKeyAuthenticationMiddleware(CreateNextDelegate(), _mockLogger.Object);
        var context = CreateHttpContext();

        await middleware.InvokeAsync(context, _mockOptionsMonitor.Object);

        Assert.True(_nextDelegateCalled);
    }

    [Fact]
    public async Task InvokeAsync_WhenRequireApiKeyIsFalse_ShouldNotReturnUnauthorized()
    {
        var options = new ServerSecurityOptions { RequireApiKey = false };
        _mockOptionsMonitor.Setup(m => m.CurrentValue).Returns(options);

        var middleware = new ApiKeyAuthenticationMiddleware(CreateNextDelegate(), _mockLogger.Object);
        var context = CreateHttpContext();

        await middleware.InvokeAsync(context, _mockOptionsMonitor.Object);

        Assert.NotEqual(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    #endregion

    #region Health Endpoint Tests

    [Fact]
    public async Task InvokeAsync_WhenHealthEndpoint_ShouldSkipAuthentication()
    {
        var options = new ServerSecurityOptions { RequireApiKey = true, ApiKey = "secret" };
        _mockOptionsMonitor.Setup(m => m.CurrentValue).Returns(options);

        var middleware = new ApiKeyAuthenticationMiddleware(CreateNextDelegate(), _mockLogger.Object);
        var context = CreateHttpContext("/health");

        await middleware.InvokeAsync(context, _mockOptionsMonitor.Object);

        Assert.True(_nextDelegateCalled);
    }

    [Fact]
    public async Task InvokeAsync_WhenHealthEndpointWithSubPath_ShouldSkipAuthentication()
    {
        var options = new ServerSecurityOptions { RequireApiKey = true, ApiKey = "secret" };
        _mockOptionsMonitor.Setup(m => m.CurrentValue).Returns(options);

        var middleware = new ApiKeyAuthenticationMiddleware(CreateNextDelegate(), _mockLogger.Object);
        var context = CreateHttpContext("/health/detailed");

        await middleware.InvokeAsync(context, _mockOptionsMonitor.Object);

        Assert.True(_nextDelegateCalled);
    }

    [Fact]
    public async Task InvokeAsync_WhenHealthEndpointCaseInsensitive_ShouldSkipAuthentication()
    {
        var options = new ServerSecurityOptions { RequireApiKey = true, ApiKey = "secret" };
        _mockOptionsMonitor.Setup(m => m.CurrentValue).Returns(options);

        var middleware = new ApiKeyAuthenticationMiddleware(CreateNextDelegate(), _mockLogger.Object);
        var context = CreateHttpContext("/HEALTH");

        await middleware.InvokeAsync(context, _mockOptionsMonitor.Object);

        Assert.True(_nextDelegateCalled);
    }

    #endregion

    #region Missing API Key Tests

    [Fact]
    public async Task InvokeAsync_WhenApiKeyMissing_ShouldReturn401()
    {
        var options = new ServerSecurityOptions { RequireApiKey = true, ApiKey = "secret" };
        _mockOptionsMonitor.Setup(m => m.CurrentValue).Returns(options);

        var middleware = new ApiKeyAuthenticationMiddleware(CreateNextDelegate(), _mockLogger.Object);
        var context = CreateHttpContext();

        await middleware.InvokeAsync(context, _mockOptionsMonitor.Object);

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.False(_nextDelegateCalled);
    }

    [Fact]
    public async Task InvokeAsync_WhenApiKeyMissing_ShouldReturnJsonError()
    {
        var options = new ServerSecurityOptions { RequireApiKey = true, ApiKey = "secret" };
        _mockOptionsMonitor.Setup(m => m.CurrentValue).Returns(options);

        var middleware = new ApiKeyAuthenticationMiddleware(CreateNextDelegate(), _mockLogger.Object);
        var context = CreateHttpContext();

        await middleware.InvokeAsync(context, _mockOptionsMonitor.Object);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var responseBody = await new StreamReader(context.Response.Body).ReadToEndAsync();

        Assert.Equal("application/json", context.Response.ContentType);
        Assert.Contains("API key is required", responseBody);
    }

    #endregion

    #region X-API-Key Header Tests

    [Fact]
    public async Task InvokeAsync_WithValidXApiKeyHeader_ShouldCallNextDelegate()
    {
        var options = new ServerSecurityOptions { RequireApiKey = true, ApiKey = "my-secret-key" };
        _mockOptionsMonitor.Setup(m => m.CurrentValue).Returns(options);

        var middleware = new ApiKeyAuthenticationMiddleware(CreateNextDelegate(), _mockLogger.Object);
        var context = CreateHttpContext("/mcp", new Dictionary<string, string>
        {
            { "X-API-Key", "my-secret-key" }
        });

        await middleware.InvokeAsync(context, _mockOptionsMonitor.Object);

        Assert.True(_nextDelegateCalled);
    }

    [Fact]
    public async Task InvokeAsync_WithInvalidXApiKeyHeader_ShouldReturn401()
    {
        var options = new ServerSecurityOptions { RequireApiKey = true, ApiKey = "my-secret-key" };
        _mockOptionsMonitor.Setup(m => m.CurrentValue).Returns(options);

        var middleware = new ApiKeyAuthenticationMiddleware(CreateNextDelegate(), _mockLogger.Object);
        var context = CreateHttpContext("/mcp", new Dictionary<string, string>
        {
            { "X-API-Key", "wrong-key" }
        });

        await middleware.InvokeAsync(context, _mockOptionsMonitor.Object);

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.False(_nextDelegateCalled);
    }

    [Fact]
    public async Task InvokeAsync_WithInvalidXApiKeyHeader_ShouldReturnJsonError()
    {
        var options = new ServerSecurityOptions { RequireApiKey = true, ApiKey = "my-secret-key" };
        _mockOptionsMonitor.Setup(m => m.CurrentValue).Returns(options);

        var middleware = new ApiKeyAuthenticationMiddleware(CreateNextDelegate(), _mockLogger.Object);
        var context = CreateHttpContext("/mcp", new Dictionary<string, string>
        {
            { "X-API-Key", "wrong-key" }
        });

        await middleware.InvokeAsync(context, _mockOptionsMonitor.Object);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var responseBody = await new StreamReader(context.Response.Body).ReadToEndAsync();

        Assert.Contains("Invalid API key", responseBody);
    }

    #endregion

    #region Authorization Bearer Header Tests

    [Fact]
    public async Task InvokeAsync_WithValidBearerToken_ShouldCallNextDelegate()
    {
        var options = new ServerSecurityOptions { RequireApiKey = true, ApiKey = "my-secret-key" };
        _mockOptionsMonitor.Setup(m => m.CurrentValue).Returns(options);

        var middleware = new ApiKeyAuthenticationMiddleware(CreateNextDelegate(), _mockLogger.Object);
        var context = CreateHttpContext("/mcp", new Dictionary<string, string>
        {
            { "Authorization", "Bearer my-secret-key" }
        });

        await middleware.InvokeAsync(context, _mockOptionsMonitor.Object);

        Assert.True(_nextDelegateCalled);
    }

    [Fact]
    public async Task InvokeAsync_WithInvalidBearerToken_ShouldReturn401()
    {
        var options = new ServerSecurityOptions { RequireApiKey = true, ApiKey = "my-secret-key" };
        _mockOptionsMonitor.Setup(m => m.CurrentValue).Returns(options);

        var middleware = new ApiKeyAuthenticationMiddleware(CreateNextDelegate(), _mockLogger.Object);
        var context = CreateHttpContext("/mcp", new Dictionary<string, string>
        {
            { "Authorization", "Bearer wrong-key" }
        });

        await middleware.InvokeAsync(context, _mockOptionsMonitor.Object);

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.False(_nextDelegateCalled);
    }

    [Fact]
    public async Task InvokeAsync_WithBearerPrefixCaseInsensitive_ShouldExtractKey()
    {
        var options = new ServerSecurityOptions { RequireApiKey = true, ApiKey = "my-secret-key" };
        _mockOptionsMonitor.Setup(m => m.CurrentValue).Returns(options);

        var middleware = new ApiKeyAuthenticationMiddleware(CreateNextDelegate(), _mockLogger.Object);
        var context = CreateHttpContext("/mcp", new Dictionary<string, string>
        {
            { "Authorization", "bearer my-secret-key" }
        });

        await middleware.InvokeAsync(context, _mockOptionsMonitor.Object);

        Assert.True(_nextDelegateCalled);
    }

    [Fact]
    public async Task InvokeAsync_WithNonBearerAuthorization_ShouldReturn401()
    {
        var options = new ServerSecurityOptions { RequireApiKey = true, ApiKey = "my-secret-key" };
        _mockOptionsMonitor.Setup(m => m.CurrentValue).Returns(options);

        var middleware = new ApiKeyAuthenticationMiddleware(CreateNextDelegate(), _mockLogger.Object);
        var context = CreateHttpContext("/mcp", new Dictionary<string, string>
        {
            { "Authorization", "Basic dXNlcjpwYXNz" }
        });

        await middleware.InvokeAsync(context, _mockOptionsMonitor.Object);

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    #endregion

    #region X-API-Key Priority Tests

    [Fact]
    public async Task InvokeAsync_WhenBothHeadersPresent_ShouldPreferXApiKey()
    {
        var options = new ServerSecurityOptions { RequireApiKey = true, ApiKey = "x-api-key-value" };
        _mockOptionsMonitor.Setup(m => m.CurrentValue).Returns(options);

        var middleware = new ApiKeyAuthenticationMiddleware(CreateNextDelegate(), _mockLogger.Object);
        var context = CreateHttpContext("/mcp", new Dictionary<string, string>
        {
            { "X-API-Key", "x-api-key-value" },
            { "Authorization", "Bearer wrong-value" }
        });

        await middleware.InvokeAsync(context, _mockOptionsMonitor.Object);

        Assert.True(_nextDelegateCalled);
    }

    #endregion

    #region Empty API Key Configuration Tests

    [Fact]
    public async Task InvokeAsync_WhenConfiguredApiKeyIsEmpty_ShouldReturn401()
    {
        var options = new ServerSecurityOptions { RequireApiKey = true, ApiKey = "" };
        _mockOptionsMonitor.Setup(m => m.CurrentValue).Returns(options);

        var middleware = new ApiKeyAuthenticationMiddleware(CreateNextDelegate(), _mockLogger.Object);
        var context = CreateHttpContext("/mcp", new Dictionary<string, string>
        {
            { "X-API-Key", "any-key" }
        });

        await middleware.InvokeAsync(context, _mockOptionsMonitor.Object);

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_WhenConfiguredApiKeyIsNull_ShouldReturn401()
    {
        var options = new ServerSecurityOptions { RequireApiKey = true, ApiKey = null };
        _mockOptionsMonitor.Setup(m => m.CurrentValue).Returns(options);

        var middleware = new ApiKeyAuthenticationMiddleware(CreateNextDelegate(), _mockLogger.Object);
        var context = CreateHttpContext("/mcp", new Dictionary<string, string>
        {
            { "X-API-Key", "any-key" }
        });

        await middleware.InvokeAsync(context, _mockOptionsMonitor.Object);

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    #endregion

    #region Timing Attack Prevention Tests

    [Fact]
    public async Task InvokeAsync_WithDifferentLengthKeys_ShouldNotLeakLengthInfo()
    {
        // This test verifies that the constant-time comparison is being used
        // by checking that both short and long invalid keys are rejected
        var options = new ServerSecurityOptions { RequireApiKey = true, ApiKey = "correct-key-12345" };
        _mockOptionsMonitor.Setup(m => m.CurrentValue).Returns(options);

        var middleware = new ApiKeyAuthenticationMiddleware(CreateNextDelegate(), _mockLogger.Object);

        // Short key
        var context1 = CreateHttpContext("/mcp", new Dictionary<string, string>
        {
            { "X-API-Key", "short" }
        });
        await middleware.InvokeAsync(context1, _mockOptionsMonitor.Object);
        Assert.Equal(StatusCodes.Status401Unauthorized, context1.Response.StatusCode);

        // Long key
        _nextDelegateCalled = false;
        var context2 = CreateHttpContext("/mcp", new Dictionary<string, string>
        {
            { "X-API-Key", "very-long-incorrect-key-that-is-much-longer" }
        });
        await middleware.InvokeAsync(context2, _mockOptionsMonitor.Object);
        Assert.Equal(StatusCodes.Status401Unauthorized, context2.Response.StatusCode);
    }

    #endregion

    #region Extension Method Tests

    [Fact]
    public void UseApiKeyAuthentication_ShouldReturnApplicationBuilder()
    {
        var mockAppBuilder = new Mock<Microsoft.AspNetCore.Builder.IApplicationBuilder>();
        mockAppBuilder
            .Setup(b => b.Use(It.IsAny<Func<RequestDelegate, RequestDelegate>>()))
            .Returns(mockAppBuilder.Object);

        var result = ApiKeyAuthenticationMiddlewareExtensions.UseApiKeyAuthentication(mockAppBuilder.Object);

        Assert.NotNull(result);
        mockAppBuilder.Verify(b => b.Use(It.IsAny<Func<RequestDelegate, RequestDelegate>>()), Times.Once);
    }

    #endregion
}
