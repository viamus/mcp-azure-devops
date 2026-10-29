using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;

namespace Viamus.Azure.Devops.Mcp.Server.Services;

/// <summary>Adds server-paginated refs to the SDK version whose generated refs method lacks paging arguments.</summary>
internal sealed class GitHistoryHttpClient : GitHttpClient
{
    public GitHistoryHttpClient(Uri baseUrl, VssCredentials credentials) : base(baseUrl, credentials) { }
    public GitHistoryHttpClient(Uri baseUrl, VssCredentials credentials, VssHttpRequestSettings settings,
        params DelegatingHandler[] handlers) : base(baseUrl, credentials, settings, handlers) { }
    public GitHistoryHttpClient(Uri baseUrl, HttpMessageHandler handler, bool disposeHandler) : base(baseUrl, handler, disposeHandler) { }

    public async Task<(IReadOnlyList<GitRef> Refs, string? ContinuationToken)> GetRefsPageAsync(
        string repository, string? project, string filter, int top, string? continuationToken, CancellationToken cancellationToken)
    {
        var projectPath = string.IsNullOrEmpty(project) ? string.Empty : Uri.EscapeDataString(project) + "/";
        var query = new List<KeyValuePair<string, string>>
        {
            new("api-version", "7.1"), new("filter", filter), new("peelTags", "true"),
            new("$top", top.ToString(System.Globalization.CultureInfo.InvariantCulture))
        };
        if (continuationToken is not null) query.Add(new("continuationToken", continuationToken));
        var queryString = string.Join("&", query.Select(pair => Uri.EscapeDataString(pair.Key) + "=" + Uri.EscapeDataString(pair.Value)));
        var uri = new Uri(BaseAddress.AbsoluteUri.TrimEnd('/') + "/" + projectPath
            + "_apis/git/repositories/" + Uri.EscapeDataString(repository) + "/refs?" + queryString);
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        using var response = await SendAsync(request, null, cancellationToken);
        var refs = await ReadContentAsAsync<List<GitRef>>(response, cancellationToken);
        var next = response.Headers.TryGetValues("x-ms-continuationtoken", out var values) ? values.FirstOrDefault() : null;
        return (refs, string.IsNullOrEmpty(next) ? null : next);
    }
}
