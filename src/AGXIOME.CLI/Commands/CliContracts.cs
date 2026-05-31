// MU-01..MU-05 — CLI Command Contracts
// All commands are unit-testable via injectable IAgxiomeApiClient + ICliOutput.
// No blocking I/O in the command layer; all ExecuteAsync → ValueTask.

namespace AGXIOME.CLI.Commands;

// ── Core result ──────────────────────────────────────────────────────────────

/// <summary>Data transfer record for the AGXIOME framework.</summary>
public sealed record CliResult(bool Success, string Output, string? Error = null, int ExitCode = 0)
{
    public static CliResult Ok(string output)          => new(true,  output, null, 0);
    public static CliResult Fail(string error, int code = 1) => new(false, string.Empty, error, code);
}

// ── Command contract ─────────────────────────────────────────────────────────

public interface ICliCommand
{
    string Name        { get; }
    string Description { get; }
    ValueTask<CliResult> ExecuteAsync(string[] args, CancellationToken ct = default);
}

// ── Lightweight API client contract (injected into commands for testability) ─

public interface IAgxiomeApiClient
{
    ValueTask<string> GetAsync(string path, CancellationToken ct = default);
    ValueTask<string> PostAsync(string path, object body, CancellationToken ct = default);
    ValueTask<string> DeleteAsync(string path, CancellationToken ct = default);
    string BaseUrl { get; }
    bool IsReachable { get; }
}

// ── Console output abstraction (replaceable in tests) ────────────────────────

public interface ICliOutput
{
    void Write(string message);
    void WriteLine(string message);
    void WriteError(string message);
}

public sealed class ConsoleCliOutput : ICliOutput
{
    public void Write(string message)      => Console.Write(message);
    public void WriteLine(string message)  => Console.WriteLine(message);
    public void WriteError(string message) => Console.Error.WriteLine(message);
}

public sealed class StringCliOutput : ICliOutput
{
    private readonly System.Text.StringBuilder _out   = new();
    private readonly System.Text.StringBuilder _err   = new();
    public string OutText   => _out.ToString();
    public string ErrorText => _err.ToString();
    public void Write(string message)      => _out.Append(message);
    public void WriteLine(string message)  => _out.AppendLine(message);
    public void WriteError(string message) => _err.AppendLine(message);
}
