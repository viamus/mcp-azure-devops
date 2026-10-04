using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Viamus.Azure.Devops.Mcp.Core.Errors;

/// <summary>Preserves SDK tool metadata and catches failures before the preview SDK hides them.</summary>
public sealed class AzureDevOpsErrorHandlingTool : DelegatingMcpServerTool
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, TypeInfoResolver = new DefaultJsonTypeInfoResolver() };
    private readonly ILogger<AzureDevOpsErrorHandlingTool> _logger;
    private readonly AIFunction? _function;

    public AzureDevOpsErrorHandlingTool(McpServerTool innerTool, ILogger<AzureDevOpsErrorHandlingTool>? logger = null, MethodInfo? method = null)
        : base(innerTool)
    {
        _logger = logger ?? NullLogger<AzureDevOpsErrorHandlingTool>.Instance;
        if (method is not null)
        {
            var options = new AIFunctionFactoryOptions
            {
                Name = innerTool.ProtocolTool.Name,
                SerializerOptions = JsonOptions,
                MarshalResult = (result, _, _) => ValueTask.FromResult(result),
                ConfigureParameterBinding = parameter => parameter.ParameterType == typeof(RequestContext<CallToolRequestParams>)
                    ? new() { ExcludeFromSchema = true, BindParameter = (_, arguments) => arguments.Context![typeof(RequestContext<CallToolRequestParams>)] }
                    : parameter.ParameterType == typeof(IMcpServer)
                        ? new() { ExcludeFromSchema = true, BindParameter = (_, arguments) => ((RequestContext<CallToolRequestParams>)arguments.Context![typeof(RequestContext<CallToolRequestParams>)]!).Server }
                        : default
            };
            _function = method.IsStatic
                ? AIFunctionFactory.Create(method, (object?)null, options)
                : AIFunctionFactory.Create(method, arguments => ActivatorUtilities.CreateInstance(arguments.Services!, method.DeclaringType!), options);
        }
    }

    public override async ValueTask<CallToolResponse> InvokeAsync(RequestContext<CallToolRequestParams> request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            // 0.2.0-preview.1's reflection tools catch and flatten exceptions themselves.
            // Invoke the attributed method directly, keeping the SDK-generated schema and metadata.
            var response = _function is null
                ? await base.InvokeAsync(request, cancellationToken)
                : await InvokeMethodAsync(request, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!MarkLegacyError(response) && response.IsError)
            {
                // A custom tool may already return a flattened error. Never expose its raw text.
                return ErrorResponse(AzureDevOpsErrorClassifier.Classify(new Exception()));
            }
            return response;
        }
        catch (Exception exception)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                if (AzureDevOpsErrorClassifier.FindCancellation(exception) is { } cancellation)
                    ExceptionDispatchInfo.Capture(cancellation).Throw();
                cancellationToken.ThrowIfCancellationRequested();
            }
            var error = AzureDevOpsErrorClassifier.Classify(exception);
            _logger.LogWarning("MCP tool {ToolName} failed ({ErrorCode}, {ExceptionType})",
                ProtocolTool.Name, error.ErrorCode, exception.GetType().Name);
            return ErrorResponse(error);
        }
    }

    private async Task<CallToolResponse> InvokeMethodAsync(RequestContext<CallToolRequestParams> request, CancellationToken cancellationToken)
    {
        var arguments = new AIFunctionArguments
        {
            Services = request.Services,
            Context = new Dictionary<object, object?> { [typeof(RequestContext<CallToolRequestParams>)] = request }
        };
        if (request.Params?.Arguments is { } suppliedArguments)
        {
            foreach (var (name, value) in suppliedArguments) arguments[name] = value;
        }
        // The public AI function factory handles JSON binding, defaults, Task/ValueTask results,
        // cancellation, and disposal. Invoke it before the MCP SDK flattens exceptions.
        var result = await _function!.InvokeAsync(arguments, cancellationToken);
        return result switch
        {
            CallToolResponse response => response,
            Content content => new() { Content = [content] },
            IEnumerable<Content> contents => new() { Content = contents.ToList() },
            null => new() { Content = [] },
            string text => new() { Content = [new Content { Type = "text", Text = text }] },
            _ => new() { Content = [new Content { Type = "text", Text = JsonSerializer.Serialize(result, JsonOptions) }] }
        };
    }
    private static CallToolResponse ErrorResponse(AzureDevOpsError error) => new()
    {
        IsError = true,
        Content = [new Content { Type = "text", Text = JsonSerializer.Serialize(error, JsonOptions) }]
    };

    private static bool MarkLegacyError(CallToolResponse response)
    {
        if (response.Content.Count != 1 || response.Content[0] is not { Type: "text", Text: { } text }) return false;
        JsonObject? payload;
        try { payload = JsonNode.Parse(text) as JsonObject; }
        catch (JsonException) { return false; }
        if (payload?["error"] is not JsonValue value || !value.TryGetValue<string>(out var message) || string.IsNullOrWhiteSpace(message)) return false;
        var notFound = message.Contains("not found", StringComparison.OrdinalIgnoreCase);
        payload["errorCode"] = notFound ? "AZURE_DEVOPS_NOT_FOUND" : "INVALID_ARGUMENT";
        payload["retryable"] = false;
        payload["httpStatusCode"] = notFound ? JsonValue.Create(404) : null;
        response.Content[0].Text = payload.ToJsonString(JsonOptions);
        response.IsError = true;
        return true;
    }
}
