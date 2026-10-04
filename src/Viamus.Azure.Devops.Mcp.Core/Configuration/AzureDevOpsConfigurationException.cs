namespace Viamus.Azure.Devops.Mcp.Server.Configuration;

/// <summary>Contains configuration field errors without including configured secrets or values.</summary>
public sealed class AzureDevOpsConfigurationException(IReadOnlyList<string> errors)
    : InvalidOperationException("Azure DevOps configuration is invalid. " + string.Join(" ", errors));
