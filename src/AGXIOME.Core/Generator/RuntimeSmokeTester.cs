using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;

namespace AGXIOME.Core.Generator;

/// <summary>
/// Boots the generated API and performs a narrow runtime smoke test.
/// </summary>
public sealed class RuntimeSmokeTester
{
    private readonly string _outputRoot;
    private readonly string _prompt;

    public RuntimeSmokeTester(string outputRoot, string prompt)
    {
        _outputRoot = outputRoot;
        _prompt = prompt;
    }

    public async Task<RuntimeSmokeResult> ExecuteAsync(CancellationToken ct = default)
    {
        var apiProject = Path.Combine(_outputRoot, "src", "Api", "Api.csproj");
        if (!File.Exists(apiProject))
        {
            return new RuntimeSmokeResult(false, false, 0, [
                new RuntimeSmokeCheck("api-project", false, "Generated Api.csproj not found")
            ]);
        }

        var port = GetFreeTcpPort();
        var baseUrl = $"http://127.0.0.1:{port}";
        var checks = new List<RuntimeSmokeCheck>();

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --no-build --project \"{apiProject}\"",
                WorkingDirectory = _outputRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.StartInfo.Environment["ASPNETCORE_URLS"] = baseUrl;

        var output = new List<string>();
        process.OutputDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) output.Add(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) output.Add(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            var started = await WaitForServerAsync(baseUrl, process, ct);
            checks.Add(new RuntimeSmokeCheck(
                "server-start",
                started,
                started ? $"Server responded on {baseUrl}" : string.Join("\n", output.TakeLast(20))));

            if (!started)
                return new RuntimeSmokeResult(false, true, port, checks);

            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

            var healthOk = await ProbeAsync(client, $"{baseUrl}/health", ct);
            checks.Add(new RuntimeSmokeCheck(
                "health-endpoint",
                healthOk,
                healthOk ? "GET /health returned success" : "GET /health did not return success"));

            var healthzOk = await ProbeAsync(client, $"{baseUrl}/healthz", ct);
            checks.Add(new RuntimeSmokeCheck(
                "healthz-endpoint",
                healthzOk,
                healthzOk ? "GET /healthz returned success" : "GET /healthz did not return success"));

            if (_prompt.Contains("incident", StringComparison.OrdinalIgnoreCase))
            {
                var incidentsOk = await ProbeAsync(client, $"{baseUrl}/incidents", ct);
                checks.Add(new RuntimeSmokeCheck(
                    "incidents-endpoint",
                    incidentsOk,
                    incidentsOk ? "GET /incidents returned success" : "GET /incidents did not return success"));
            }

            return new RuntimeSmokeResult(checks.All(c => c.Passed), true, port, checks);
        }
        finally
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch
            {
                // best effort cleanup
            }
        }
    }

    private static async Task<bool> WaitForServerAsync(string baseUrl, Process process, CancellationToken ct)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var startedAt = DateTime.UtcNow;
        while (DateTime.UtcNow - startedAt < TimeSpan.FromSeconds(60))
        {
            ct.ThrowIfCancellationRequested();

            if (process.HasExited)
                return false;

            try
            {
                using var response = await client.GetAsync($"{baseUrl}/health", ct);
                if ((int)response.StatusCode < 500)
                    return true;
            }
            catch
            {
                // keep polling until timeout
            }

            await Task.Delay(1000, ct);
        }

        return false;
    }

    private static async Task<bool> ProbeAsync(HttpClient client, string url, CancellationToken ct)
    {
        try
        {
            using var response = await client.GetAsync(url, ct);
            return response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Created;
        }
        catch
        {
            return false;
        }
    }

    private static int GetFreeTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}