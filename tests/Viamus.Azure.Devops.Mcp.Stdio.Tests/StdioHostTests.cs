using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using Viamus.Azure.Devops.Mcp.Core;
using Viamus.Azure.Devops.Mcp.Server.Configuration;
using Viamus.Azure.Devops.Mcp.Stdio;

namespace Viamus.Azure.Devops.Mcp.Stdio.Tests;

public sealed class StdioHostTests
{
    [Fact]
    public void Host_UsesExecutableDirectoryAndSendsEveryLogLevelToStandardError()
    {
        var builder = StdioHost.CreateBuilder([
            "--AzureDevOps:OrganizationUrl=https://dev.azure.com/example",
            "--AzureDevOps:PersonalAccessToken=fake-test-token"
        ]);
        using var host = builder.Build();

        Assert.Equal(Path.GetFullPath(AppContext.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar),
            Path.GetFullPath(builder.Environment.ContentRootPath).TrimEnd(Path.DirectorySeparatorChar));
        var consoleOptions = host.Services.GetRequiredService<IOptions<ConsoleLoggerOptions>>().Value;
        Assert.Equal(LogLevel.Trace, consoleOptions.LogToStandardErrorThreshold);
        Assert.Equal("fake-test-token", host.Services.GetRequiredService<IOptions<AzureDevOpsOptions>>().Value.PersonalAccessToken);
    }

    [Fact]
    public void Registration_RejectsMissingCredentialsWithActionableEnvironmentSettingNames()
    {
        var configuration = new ConfigurationBuilder().Build();
        var exception = Assert.Throws<AzureDevOpsConfigurationException>(
            () => new ServiceCollection().AddAzureDevOpsMcp(configuration));

        Assert.Contains("AzureDevOps:OrganizationUrl", exception.Message);
        Assert.Contains("AzureDevOps__PersonalAccessToken", exception.Message);
    }

    [Theory]
    [InlineData("not-a-url-secret")]
    [InlineData("https://secret-user:secret-pat@dev.azure.com/example")]
    [InlineData("https://dev.azure.com/example?token=secret-pat")]
    [InlineData("file:///secret-pat")]
    public void Validation_RejectsUnsafeUrlWithoutEchoingAnyConfiguredValues(string url)
    {
        var options = new AzureDevOpsOptions
        {
            OrganizationUrl = url,
            PersonalAccessToken = "secret-pat"
        };

        var errors = AzureDevOpsConfigurationValidator.Validate(options);

        Assert.Single(errors);
        Assert.Contains("AzureDevOps:OrganizationUrl", errors[0]);
        Assert.DoesNotContain(url, errors[0]);
        Assert.DoesNotContain("secret-pat", errors[0]);
    }

    [Fact]
    public void Validation_IdentifiesInvalidOrganizationByIndexWithoutEchoingItsAlias()
    {
        var options = new AzureDevOpsOptions
        {
            Organizations = [new AzureDevOpsOrganizationOptions { Name = "secret-alias" }]
        };

        var errors = AzureDevOpsConfigurationValidator.Validate(options);

        Assert.Equal(2, errors.Count);
        Assert.All(errors, message => Assert.Contains("AzureDevOps:Organizations:0", message));
        Assert.All(errors, message => Assert.DoesNotContain("secret-alias", message));
    }

    [Fact]
    public void Validation_AcceptsLegacyAndMultipleOrganizationSettings()
    {
        var options = new AzureDevOpsOptions
        {
            OrganizationUrl = "https://dev.azure.com/legacy",
            PersonalAccessToken = "fake-legacy-token",
            Organizations = [new AzureDevOpsOrganizationOptions
            {
                Name = "another",
                OrganizationUrl = "http://localhost:1234/collection",
                PersonalAccessToken = "fake-another-token"
            }]
        };

        Assert.Empty(AzureDevOpsConfigurationValidator.Validate(options));
    }
}
