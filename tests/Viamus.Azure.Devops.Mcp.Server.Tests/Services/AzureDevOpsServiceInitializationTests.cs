using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Viamus.Azure.Devops.Mcp.Core.Errors;
using Viamus.Azure.Devops.Mcp.Server.Configuration;
using Viamus.Azure.Devops.Mcp.Server.Services;

namespace Viamus.Azure.Devops.Mcp.Server.Tests.Services;

public class AzureDevOpsServiceInitializationTests
{
    [Fact]
    public void ConstructorAndDispose_DoNotAuthenticateConfiguredOrganizations()
    {
        // Any eager discovery would fail against these unreachable loopback ports.
        using var service = new AzureDevOpsService(Options.Create(new AzureDevOpsOptions
        {
            Organizations =
            [
                new() { Name = "first", OrganizationUrl = "http://127.0.0.1:1/first", PersonalAccessToken = "fake-first" },
                new() { Name = "second", OrganizationUrl = "http://localhost:1/second", PersonalAccessToken = "fake-second" }
            ]
        }), NullLogger<AzureDevOpsService>.Instance, new AzureDevOpsOrganizationContextAccessor());
    }

    [Fact]
    public async Task InsecureHttpDiscovery_ReturnsSafeGenericFailureWithoutClaimingPatExpiry()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var app = builder.Build();
        app.Run(context =>
        {
            context.Response.StatusCode = 401;
            return Task.CompletedTask;
        });
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var service = new AzureDevOpsService(Options.Create(new AzureDevOpsOptions
        {
            OrganizationUrl = address,
            PersonalAccessToken = "fake"
        }), NullLogger<AzureDevOpsService>.Instance, new AzureDevOpsOrganizationContextAccessor());
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var exception = await Record.ExceptionAsync(() => service.GetRepositoriesAsync(cancellationToken: timeout.Token));
        Assert.NotNull(exception);
        var error = AzureDevOpsErrorClassifier.Classify(exception);
        Assert.Equal("UNEXPECTED_ERROR", error.ErrorCode);
        Assert.DoesNotContain("Basic authentication", error.Error);
        Assert.DoesNotContain("expired", error.Error);
    }
    [Fact]
    public async Task UnknownOrganization_IsExplicitConfigurationFailureWithoutEchoingInput()
    {
        var accessor = new AzureDevOpsOrganizationContextAccessor();
        using var service = new AzureDevOpsService(Options.Create(new AzureDevOpsOptions
        {
            OrganizationUrl = "http://127.0.0.1:1/first",
            PersonalAccessToken = "fake"
        }), NullLogger<AzureDevOpsService>.Instance, accessor);
        using var scope = accessor.Use("PAT-secret unconfigured organization");
        var exception = await Assert.ThrowsAsync<AzureDevOpsConfigurationException>(() => service.GetRepositoriesAsync());
        Assert.Equal("CONFIGURATION_ERROR", AzureDevOpsErrorClassifier.Classify(exception).ErrorCode);
        Assert.DoesNotContain("PAT-secret", exception.Message);
    }
}
