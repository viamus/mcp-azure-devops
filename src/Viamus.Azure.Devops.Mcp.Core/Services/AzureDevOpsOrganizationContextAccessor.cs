namespace Viamus.Azure.Devops.Mcp.Server.Services;

/// <summary>
/// Async-local implementation for selecting an Azure DevOps organization per tool call.
/// </summary>
public sealed class AzureDevOpsOrganizationContextAccessor : IAzureDevOpsOrganizationContextAccessor
{
    private readonly AsyncLocal<string?> _currentOrganization = new();

    public string? CurrentOrganization => _currentOrganization.Value;

    public IDisposable Use(string? organization)
    {
        var previous = _currentOrganization.Value;
        _currentOrganization.Value = string.IsNullOrWhiteSpace(organization)
            ? null
            : organization.Trim();

        return new Scope(this, previous);
    }

    private sealed class Scope(AzureDevOpsOrganizationContextAccessor accessor, string? previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            accessor._currentOrganization.Value = previous;
            _disposed = true;
        }
    }
}
