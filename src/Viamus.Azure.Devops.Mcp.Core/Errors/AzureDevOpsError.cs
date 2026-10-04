namespace Viamus.Azure.Devops.Mcp.Core.Errors;

/// <summary>A safe, actionable tool failure that never contains upstream exception text.</summary>
public sealed record AzureDevOpsError(string Error, string ErrorCode, bool Retryable, int? HttpStatusCode = null);
