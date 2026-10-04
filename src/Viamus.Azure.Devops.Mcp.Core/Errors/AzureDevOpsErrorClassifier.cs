using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Viamus.Azure.Devops.Mcp.Server.Configuration;

namespace Viamus.Azure.Devops.Mcp.Core.Errors;

public static class AzureDevOpsErrorClassifier
{
    public static AzureDevOpsError Classify(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var exceptions = EnumerateExceptions(exception).ToArray();
        var statuses = exceptions.Select(GetStatusCode).Where(value => value.HasValue).ToArray();
        var status = statuses.FirstOrDefault(value => value is 401 or 403) ?? statuses.FirstOrDefault();
        if (status != 403 && exceptions.Any(ex => ex is VssUnauthorizedException or VssAuthenticationException)) return AuthenticationFailure();
        if (status.HasValue)
        {
            return status.Value switch
            {
                401 => AuthenticationFailure(),
                403 => new("Azure DevOps denied access. Check the PAT scopes and the user's organization, project, and resource permissions.", "AZURE_DEVOPS_FORBIDDEN", false, 403),
                404 => new("The Azure DevOps resource was not found. Check the organization, project, resource ID, and access permissions.", "AZURE_DEVOPS_NOT_FOUND", false, 404),
                400 or 422 => new("Azure DevOps rejected the request. Check the tool arguments, field names, and field values.", "INVALID_ARGUMENT", false, status),
                409 => new("Azure DevOps reported a conflict. Read the current resource state and resolve the conflict before trying again.", "AZURE_DEVOPS_CONFLICT", false, 409),
                408 or 504 => TimeoutFailure(status),
                429 => new("Azure DevOps is rate limiting requests. Wait before trying again; verify the current state before repeating a write.", "AZURE_DEVOPS_RATE_LIMITED", true, 429),
                >= 500 => new("Azure DevOps is temporarily unavailable. Try again later; verify the current state before repeating a write.", "AZURE_DEVOPS_UNAVAILABLE", true, status),
                _ => new("Azure DevOps could not complete the request. Check the request and the service status.", "UNEXPECTED_ERROR", false, status)
            };
        }

        if (exceptions.Any(ex => ex is VssUnauthorizedException or VssAuthenticationException))
        {
            return AuthenticationFailure();
        }
        if (exceptions.Any(ex => ex is AzureDevOpsConfigurationException or OptionsValidationException))
        {
            return new("The requested Azure DevOps organization is not configured correctly. Check AzureDevOps:Organizations, DefaultOrganization, organization URLs, and PAT configuration, then restart the host if configuration changed.", "CONFIGURATION_ERROR", false);
        }
        if (exceptions.Any(ex => ex is VssResourceNotFoundException))
        {
            return new("The required Azure DevOps API location is unavailable. Check the organization URL and the server API support or version.", "AZURE_DEVOPS_API_UNAVAILABLE", false);
        }
        if (exceptions.Any(ex => ex is TimeoutException or OperationCanceledException || ex is WebException { Status: WebExceptionStatus.Timeout }))
        {
            return TimeoutFailure();
        }
        if (exceptions.Any(ex => ex is HttpRequestException or SocketException or WebException))
        {
            return new("The MCP could not connect to Azure DevOps. Check the network, DNS, proxy, and organization URL; verify the current state before repeating a write.", "AZURE_DEVOPS_NETWORK_ERROR", true);
        }
        if (exceptions.Any(ex => ex is ArgumentException or FormatException or JsonException))
        {
            return new("The tool arguments are invalid. Check the required arguments, formats, and allowed values.", "INVALID_ARGUMENT", false);
        }

        return new("An unexpected error prevented the tool from completing. Review the server diagnostics; verify the current state before repeating a write.", "UNEXPECTED_ERROR", false);
    }

    /// <summary>Only an actual 404 may be converted to an optional resource's null result.</summary>
    public static bool IsNotFound(Exception exception)
    {
        var exceptions = EnumerateExceptions(exception).ToArray();
        var statuses = exceptions.Select(GetStatusCode).Where(value => value.HasValue).ToArray();
        var status = statuses.FirstOrDefault(value => value is 401 or 403) ?? statuses.FirstOrDefault();
        if (status is 401 or 403 || exceptions.Any(ex => ex is VssUnauthorizedException or VssAuthenticationException or OperationCanceledException)) return false;
        if (status.HasValue) return status == 404;
        if (exceptions.Any(ex => ex is VssUnauthorizedException or VssAuthenticationException or OperationCanceledException or HttpRequestException or WebException or SocketException)) return false;
        return false;
    }

    public static OperationCanceledException? FindCancellation(Exception exception) =>
        EnumerateExceptions(exception).OfType<OperationCanceledException>().FirstOrDefault();

    private static AzureDevOpsError AuthenticationFailure() => new(
        "Azure DevOps authentication failed. The PAT may be expired, revoked, or invalid. Replace the PAT for the selected organization, check access, and restart the host if configuration changed.",
        "AZURE_DEVOPS_AUTHENTICATION_FAILED", false, 401);

    private static AzureDevOpsError TimeoutFailure(int? status = null) => new(
        "The Azure DevOps request timed out. Try again later; verify the current state before repeating a write.",
        "AZURE_DEVOPS_TIMEOUT", true, status);

    private static int? GetStatusCode(Exception exception) => exception switch
    {
        VssServiceResponseException response => (int)response.HttpStatusCode,
        HttpRequestException { StatusCode: { } status } => (int)status,
        WebException { Response: HttpWebResponse response } => (int)response.StatusCode,
        _ => null
    };

    private static IEnumerable<Exception> EnumerateExceptions(Exception exception)
    {
        var pending = new Stack<Exception>();
        var visited = new HashSet<Exception>(ReferenceEqualityComparer.Instance);
        pending.Push(exception);
        while (pending.TryPop(out var current))
        {
            if (!visited.Add(current)) continue;
            yield return current;
            if (current is AggregateException aggregate)
            {
                foreach (var inner in aggregate.InnerExceptions.Reverse()) pending.Push(inner);
            }
            else if (current.InnerException is { } inner)
            {
                pending.Push(inner);
            }
        }
    }
}
