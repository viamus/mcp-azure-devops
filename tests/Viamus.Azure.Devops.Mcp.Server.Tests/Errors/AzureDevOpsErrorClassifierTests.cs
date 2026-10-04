using System.Net;
using System.Reflection;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Viamus.Azure.Devops.Mcp.Core.Errors;
using Viamus.Azure.Devops.Mcp.Server.Configuration;

namespace Viamus.Azure.Devops.Mcp.Server.Tests.Errors;

public class AzureDevOpsErrorClassifierTests
{
    [Theory]
    [InlineData(401, "AZURE_DEVOPS_AUTHENTICATION_FAILED", false)]
    [InlineData(403, "AZURE_DEVOPS_FORBIDDEN", false)]
    [InlineData(404, "AZURE_DEVOPS_NOT_FOUND", false)]
    [InlineData(400, "INVALID_ARGUMENT", false)]
    [InlineData(409, "AZURE_DEVOPS_CONFLICT", false)]
    [InlineData(422, "INVALID_ARGUMENT", false)]
    [InlineData(429, "AZURE_DEVOPS_RATE_LIMITED", true)]
    [InlineData(500, "AZURE_DEVOPS_UNAVAILABLE", true)]
    [InlineData(503, "AZURE_DEVOPS_UNAVAILABLE", true)]
    [InlineData(408, "AZURE_DEVOPS_TIMEOUT", true)]
    [InlineData(504, "AZURE_DEVOPS_TIMEOUT", true)]
    public void Classify_StatusFailure_ReturnsSafeActionableError(int status, string code, bool retryable)
    {
        var exception = new VssServiceResponseException((HttpStatusCode)status, "PAT-secret upstream body", null!);
        var result = AzureDevOpsErrorClassifier.Classify(new TargetInvocationException(exception));
        Assert.Equal(code, result.ErrorCode);
        Assert.Equal(retryable, result.Retryable);
        Assert.Equal(status, result.HttpStatusCode);
        Assert.DoesNotContain("PAT-secret", result.Error);
    }

    [Fact]
    public void Classify_WrappedAuthenticationFailure_DoesNotClaimConfirmedExpiry()
    {
        var exception = new TargetInvocationException(new AggregateException(new VssUnauthorizedException("PAT-secret not found")));
        var result = AzureDevOpsErrorClassifier.Classify(exception);
        Assert.Equal("AZURE_DEVOPS_AUTHENTICATION_FAILED", result.ErrorCode);
        Assert.Contains("may be expired, revoked, or invalid", result.Error);
        Assert.False(AzureDevOpsErrorClassifier.IsNotFound(exception));
        Assert.DoesNotContain("PAT-secret", result.Error);
    }

    [Theory]
    [InlineData(401, false)]
    [InlineData(403, false)]
    [InlineData(404, true)]
    [InlineData(500, false)]
    public void IsNotFound_UsesHttpStatusRatherThanExceptionMessage(int status, bool expected)
    {
        var exception = new HttpRequestException("does not exist; not found", null, (HttpStatusCode)status);
        Assert.Equal(expected, AzureDevOpsErrorClassifier.IsNotFound(exception));
    }

    [Fact]
    public void IsNotFound_MessageAloneCannotHideUnknownOrAuthorizationFailures()
    {
        Assert.False(AzureDevOpsErrorClassifier.IsNotFound(new InvalidOperationException("not found")));
        Assert.False(AzureDevOpsErrorClassifier.IsNotFound(new VssAuthenticationException("does not exist")));
    }

    [Fact]
    public void IsNotFound_TypedApiLocationFailureIsNeverResourceNotFound()
    {
        var missing = new VssResourceNotFoundException("missing");
        Assert.False(AzureDevOpsErrorClassifier.IsNotFound(missing));
        Assert.Equal("AZURE_DEVOPS_API_UNAVAILABLE", AzureDevOpsErrorClassifier.Classify(missing).ErrorCode);
        Assert.False(AzureDevOpsErrorClassifier.IsNotFound(new TargetInvocationException(missing)));
        Assert.False(AzureDevOpsErrorClassifier.IsNotFound(new HttpRequestException("wrapper", missing)));
        Assert.False(AzureDevOpsErrorClassifier.IsNotFound(new AggregateException(missing, new VssUnauthorizedException("secret"))));
    }

    [Fact]
    public void AuthenticationFailureTakesPrecedenceOverMisleadingOuterNotFound()
    {
        var exception = new HttpRequestException("not found", new VssUnauthorizedException("secret"), HttpStatusCode.NotFound);
        Assert.False(AzureDevOpsErrorClassifier.IsNotFound(exception));
        Assert.Equal("AZURE_DEVOPS_AUTHENTICATION_FAILED", AzureDevOpsErrorClassifier.Classify(exception).ErrorCode);
    }
    [Fact]
    public void Classify_NetworkTimeoutAndArguments_HaveDifferentRemediation()
    {
        Assert.Equal("AZURE_DEVOPS_NETWORK_ERROR", AzureDevOpsErrorClassifier.Classify(new HttpRequestException("secret")).ErrorCode);
        Assert.Equal("AZURE_DEVOPS_TIMEOUT", AzureDevOpsErrorClassifier.Classify(new TimeoutException("secret")).ErrorCode);
        Assert.Equal("AZURE_DEVOPS_TIMEOUT", AzureDevOpsErrorClassifier.Classify(new TaskCanceledException("secret")).ErrorCode);
        Assert.Equal("INVALID_ARGUMENT", AzureDevOpsErrorClassifier.Classify(new ArgumentException("secret")).ErrorCode);
    }

    [Fact]
    public void Classify_ConfigurationIsExplicitAndUnexpectedFailuresRemainGeneric()
    {
        Assert.Equal("CONFIGURATION_ERROR", AzureDevOpsErrorClassifier.Classify(new AzureDevOpsConfigurationException(["secret"])).ErrorCode);
        var unknown = AzureDevOpsErrorClassifier.Classify(new InvalidOperationException("secret PAT raw response"));
        Assert.Equal("UNEXPECTED_ERROR", unknown.ErrorCode);
        Assert.DoesNotContain("secret", unknown.Error);
        Assert.False(unknown.Retryable);
    }
}
