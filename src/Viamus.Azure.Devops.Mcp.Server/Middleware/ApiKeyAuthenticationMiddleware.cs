using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Viamus.Azure.Devops.Mcp.Server.Configuration;

namespace Viamus.Azure.Devops.Mcp.Server.Middleware;

/// <summary>
/// Middleware for API key authentication.
/// </summary>
public class ApiKeyAuthenticationMiddleware
{
    private const string ApiKeyHeaderName = "X-API-Key";
    private const string AuthorizationHeaderName = "Authorization";
    private const string BearerPrefix = "Bearer ";

    private readonly RequestDelegate _next;
    private readonly ILogger<ApiKeyAuthenticationMiddleware> _logger;

    public ApiKeyAuthenticationMiddleware(RequestDelegate next, ILogger<ApiKeyAuthenticationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IOptionsMonitor<ServerSecurityOptions> optionsMonitor)
    {
        var options = optionsMonitor.CurrentValue;

        // Skip authentication if not required
        if (!options.RequireApiKey)
        {
            await _next(context);
            return;
        }

        // Allow health endpoint without authentication
        if (context.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        // Extract API key from headers
        var apiKey = ExtractApiKey(context.Request);

        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogWarning("API key missing in request to {Path}", context.Request.Path);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"error\":\"API key is required\"}");
            return;
        }

        // Validate API key with constant-time comparison
        if (string.IsNullOrEmpty(options.ApiKey) || !ConstantTimeEquals(apiKey, options.ApiKey))
        {
            _logger.LogWarning("Invalid API key provided for request to {Path}", context.Request.Path);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"error\":\"Invalid API key\"}");
            return;
        }

        await _next(context);
    }

    private static string? ExtractApiKey(HttpRequest request)
    {
        // Try X-API-Key header first
        if (request.Headers.TryGetValue(ApiKeyHeaderName, out var apiKeyHeader))
        {
            return apiKeyHeader.ToString();
        }

        // Try Authorization: Bearer header
        if (request.Headers.TryGetValue(AuthorizationHeaderName, out var authHeader))
        {
            var authValue = authHeader.ToString();
            if (authValue.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return authValue[BearerPrefix.Length..];
            }
        }

        return null;
    }

    /// <summary>
    /// Performs a constant-time comparison to prevent timing attacks.
    /// </summary>
    private static bool ConstantTimeEquals(string a, string b)
    {
        var aBytes = Encoding.UTF8.GetBytes(a);
        var bBytes = Encoding.UTF8.GetBytes(b);
        return CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
    }
}

/// <summary>
/// Extension methods for API key authentication middleware.
/// </summary>
public static class ApiKeyAuthenticationMiddlewareExtensions
{
    public static IApplicationBuilder UseApiKeyAuthentication(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ApiKeyAuthenticationMiddleware>();
    }
}
