using Viamus.Azure.Devops.Mcp.Server.Configuration;

namespace Viamus.Azure.Devops.Mcp.Server.Tests.Configuration;

public class ServerSecurityOptionsTests
{
    [Fact]
    public void SectionName_ShouldBeServerSecurity()
    {
        Assert.Equal("ServerSecurity", ServerSecurityOptions.SectionName);
    }

    [Fact]
    public void RequireApiKey_ShouldDefaultToFalse()
    {
        var options = new ServerSecurityOptions();

        Assert.False(options.RequireApiKey);
    }

    [Fact]
    public void ApiKey_ShouldDefaultToNull()
    {
        var options = new ServerSecurityOptions();

        Assert.Null(options.ApiKey);
    }

    [Fact]
    public void ApiKey_ShouldBeSettable()
    {
        var options = new ServerSecurityOptions
        {
            ApiKey = "test-api-key"
        };

        Assert.Equal("test-api-key", options.ApiKey);
    }

    [Fact]
    public void RequireApiKey_ShouldBeSettable()
    {
        var options = new ServerSecurityOptions
        {
            RequireApiKey = true
        };

        Assert.True(options.RequireApiKey);
    }

    [Fact]
    public void AllProperties_ShouldBeSettable()
    {
        var options = new ServerSecurityOptions
        {
            ApiKey = "my-secret-key",
            RequireApiKey = true
        };

        Assert.Equal("my-secret-key", options.ApiKey);
        Assert.True(options.RequireApiKey);
    }
}
