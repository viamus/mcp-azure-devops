using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using ModelContextProtocol.Server;
using Viamus.Azure.Devops.Mcp.Server.Tools;

namespace Viamus.Azure.Devops.Mcp.Stdio.Tests;

public sealed class StdioProtocolTests
{
    [Fact]
    public async Task Host_EmitsOnlyJsonRpcOnStdout_WhenListingToolsWithTraceLogging()
    {
        const string fakePat = "stdio-protocol-test-secret";
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var startInfo = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Path.GetTempPath()
        };
        startInfo.ArgumentList.Add(GetStdioAssemblyPath());
        foreach (var name in startInfo.Environment.Keys.ToArray())
        {
            if (name.StartsWith("AzureDevOps__", StringComparison.OrdinalIgnoreCase))
                startInfo.Environment.Remove(name);
        }
        startInfo.Environment["AzureDevOps__OrganizationUrl"] = "http://127.0.0.1:1/unused";
        startInfo.Environment["AzureDevOps__PersonalAccessToken"] = fakePat;
        startInfo.Environment["Logging__LogLevel__Default"] = "Trace";
        startInfo.Environment["Logging__LogLevel__ModelContextProtocol"] = "Trace";

        using var process = Process.Start(startInfo)!;
        var initialized = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        var toolList = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        var messageCount = 0;
        var stdoutTask = ReadProtocolOutput();
        var stderrTask = process.StandardError.ReadToEndAsync(timeout.Token);
        try
        {
            process.StandardInput.AutoFlush = true;
            await process.StandardInput.WriteLineAsync("""{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-03-26","capabilities":{},"clientInfo":{"name":"raw-protocol-test","version":"1.0.0"}}}""");
            var initializeResponse = await initialized.Task.WaitAsync(timeout.Token);
            Assert.True(initializeResponse.TryGetProperty("result", out _));

            await process.StandardInput.WriteLineAsync("""{"jsonrpc":"2.0","method":"notifications/initialized"}""");
            await process.StandardInput.WriteLineAsync("""{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}""");
            var listResponse = await toolList.Task.WaitAsync(timeout.Token);
            var names = listResponse.GetProperty("result").GetProperty("tools").EnumerateArray()
                .Select(tool => tool.GetProperty("name").GetString()).OrderBy(name => name).ToArray();
            var expectedNames = typeof(GitTools).Assembly.GetTypes().SelectMany(type => type.GetMethods())
                .Select(method => method.GetCustomAttribute<McpServerToolAttribute>())
                .Where(attribute => attribute != null).Select(attribute => attribute!.Name)
                .OrderBy(name => name).ToArray();
            Assert.Equal(expectedNames, names);

            process.StandardInput.Close();
            await process.WaitForExitAsync(timeout.Token);
            await stdoutTask;
            Assert.Equal(0, process.ExitCode);
            Assert.True(messageCount >= 2);
            Assert.NotEmpty(await stderrTask);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
            }
        }

        async Task ReadProtocolOutput()
        {
            try
            {
                while (await process.StandardOutput.ReadLineAsync(timeout.Token) is { } line)
                {
                    Assert.DoesNotContain(fakePat, line);
                    using var document = JsonDocument.Parse(line);
                    var message = document.RootElement;
                    Assert.Equal("2.0", message.GetProperty("jsonrpc").GetString());
                    messageCount++;
                    if (message.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.Number)
                    {
                        if (id.GetInt32() == 1) initialized.TrySetResult(message.Clone());
                        if (id.GetInt32() == 2) toolList.TrySetResult(message.Clone());
                    }
                }
                initialized.TrySetException(new InvalidOperationException("STDIO ended before initialize responded."));
                toolList.TrySetException(new InvalidOperationException("STDIO ended before tools/list responded."));
            }
            catch (Exception exception)
            {
                initialized.TrySetException(exception);
                toolList.TrySetException(exception);
                throw;
            }
        }
    }

    private static string GetStdioAssemblyPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Solution.slnx")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        var configuration = typeof(StdioProtocolTests).Assembly
            .GetCustomAttribute<AssemblyConfigurationAttribute>()!.Configuration;
        var path = Path.Combine(directory!.FullName, "src", "Viamus.Azure.Devops.Mcp.Stdio", "bin",
            configuration, "net10.0", "Viamus.Azure.Devops.Mcp.Stdio.dll");
        Assert.True(File.Exists(path), "Build the STDIO project before running the process protocol test.");
        return path;
    }
}
