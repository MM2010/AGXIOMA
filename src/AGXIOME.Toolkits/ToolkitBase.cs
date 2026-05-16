// TB-00 — Toolkit Infrastructure: IToolkit, ToolkitBase, ToolkitAttribute, ToolkitResult
// All 161 toolkits derive from ToolkitBase and carry [Toolkit] metadata attributes.
// Execution is async-first; each toolkit returns a ToolkitResult with typed output.

using System.Reflection;
using AGXIOME.Shared.Errors;

namespace AGXIOME.Toolkits;

// ─── Enums ───────────────────────────────────────────────────────────────────

public enum ToolkitCategory
{
    Communication    = 1,
    Productivity     = 2,
    DataAnalysis     = 3,
    Development      = 4,
    AiMl             = 5,
    Finance          = 6,
    CloudInfra       = 7,
    WebSocial        = 8,
    SearchKnowledge  = 9,
    FileStorage      = 10,
    GamingCreative   = 11,
    Enterprise       = 12
}

public enum ToolkitResultStatus { Ok = 0, Error = 1, Skipped = 2, RateLimited = 3, Unauthorized = 4 }

// ─── Attribute ────────────────────────────────────────────────────────────────

/// <summary>
/// Marks a class as a toolkit and provides metadata for auto-discovery via reflection.
/// Usage: [Toolkit("slack", "Slack", ToolkitCategory.Communication)]
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ToolkitAttribute(string id, string displayName, ToolkitCategory category) : Attribute
{
    public string          Id          { get; } = id;
    public string          DisplayName { get; } = displayName;
    public ToolkitCategory Category    { get; } = category;
}

// ─── Value Objects ────────────────────────────────────────────────────────────

public sealed record ToolkitResult(
    ToolkitResultStatus Status,
    object?             Data,
    string?             Error      = null,
    TimeSpan?           Duration   = null,
    Dictionary<string, string>? Metadata = null)
{
    public static ToolkitResult Success(object? data = null, TimeSpan? duration = null)
        => new(ToolkitResultStatus.Ok, data, null, duration);

    public static ToolkitResult Fail(string error)
        => new(ToolkitResultStatus.Error, null, error);

    public static ToolkitResult NotConfigured(string toolkit)
        => new(ToolkitResultStatus.Skipped, null, $"Toolkit '{toolkit}' is not configured.");
}

public sealed record ToolkitConfig(
    string  ToolkitId,
    Dictionary<string, string> Settings);

// ─── Interface ────────────────────────────────────────────────────────────────

public interface IToolkit
{
    string          Id       { get; }
    string          Name     { get; }
    ToolkitCategory Category { get; }
    bool            IsConfigured { get; }
    Task<ToolkitResult> ExecuteAsync(string action, Dictionary<string, object?> parameters, CancellationToken ct = default);
}

// ─── Base Class ───────────────────────────────────────────────────────────────

/// <summary>
/// Abstract base for all AGXIOME toolkits.
/// Subclasses override ExecuteCoreAsync and declare their actions via HandleAction.
/// Configuration is injected via ToolkitConfig or environment variables.
/// </summary>
public abstract class ToolkitBase : IToolkit
{
    protected readonly ToolkitConfig? Config;

    protected ToolkitBase(ToolkitConfig? config = null) => Config = config;

    public abstract string          Id       { get; }
    public abstract string          Name     { get; }
    public abstract ToolkitCategory Category { get; }

    public virtual bool IsConfigured =>
        Config is not null && Config.Settings.Count > 0;

    public async Task<ToolkitResult> ExecuteAsync(
        string action,
        Dictionary<string, object?> parameters,
        CancellationToken ct = default)
    {
        if (!IsConfigured) return ToolkitResult.NotConfigured(Id);
        var start = DateTimeOffset.UtcNow;
        try
        {
            var result = await ExecuteCoreAsync(action, parameters, ct);
            return result with { Duration = DateTimeOffset.UtcNow - start };
        }
        catch (OperationCanceledException)
        {
            return FailureResult(AgxiomeException.FromCode(AgxiomeErrorCode.OperationCancelled));
        }
        catch (AgxiomeException ex)
        {
            return FailureResult(ex);
        }
        catch (Exception ex)
        {
            return FailureResult(AgxiomeToolkitException.ExecutionFailed(Id, action, ex));
        }
    }

    protected abstract Task<ToolkitResult> ExecuteCoreAsync(
        string action,
        Dictionary<string, object?> parameters,
        CancellationToken ct);

    protected static T? GetParam<T>(Dictionary<string, object?> parameters, string key) =>
        parameters.TryGetValue(key, out var val) && val is T typed ? typed : default;

    protected T GetRequiredParam<T>(Dictionary<string, object?> parameters, string key)
    {
        if (parameters.TryGetValue(key, out var val) && val is T typed)
            return typed;

        throw AgxiomeToolkitException.MissingParameter(Id, key);
    }

    protected string? GetSetting(string key) =>
        Config?.Settings.TryGetValue(key, out var val) == true ? val : null;

    protected string GetRequiredSetting(string key)
        => GetSetting(key) ?? throw AgxiomeToolkitException.MissingSetting(Id, key);

    private static ToolkitResult FailureResult(AgxiomeException ex)
        => new(
            ex.ErrorCode == AgxiomeErrorCode.LLM_RateLimited ? ToolkitResultStatus.RateLimited :
            ex.ErrorCode == AgxiomeErrorCode.LLM_AuthenticationFailed || ex.ErrorCode == AgxiomeErrorCode.SE_AuthenticationFailed
                ? ToolkitResultStatus.Unauthorized
                : ToolkitResultStatus.Error,
            null,
            ex.UserMessage,
            null,
            new Dictionary<string, string>
            {
                ["errorCode"] = ErrorCatalog.GetCode(ex.ErrorCode),
                ["numericCode"] = ex.NumericCode.ToString(),
                ["category"] = ex.Category,
            });
}

// ─── Toolkit Registry ─────────────────────────────────────────────────────────

/// <summary>
/// Discovers and manages all registered toolkits.
/// Auto-discovery via [Toolkit] attribute on assemblies passed to Scan().
/// </summary>
public sealed class ToolkitRegistry
{
    private readonly Dictionary<string, IToolkit> _toolkits = [];

    public ToolkitRegistry Register(IToolkit toolkit)
    {
        _toolkits[toolkit.Id] = toolkit;
        return this;
    }

    public IToolkit? Find(string id) =>
        _toolkits.TryGetValue(id, out var t) ? t : null;

    public IReadOnlyList<IToolkit> All => _toolkits.Values.ToList();

    public IReadOnlyList<IToolkit> ByCategory(ToolkitCategory category) =>
        _toolkits.Values.Where(t => t.Category == category).ToList();

    /// <summary>Auto-discover toolkit types from an assembly using [Toolkit] attributes.</summary>
    public static IReadOnlyList<Type> DiscoverTypes(Assembly assembly) =>
        assembly.GetTypes()
                .Where(t => t.GetCustomAttribute<ToolkitAttribute>() is not null && !t.IsAbstract)
                .ToList();
}
