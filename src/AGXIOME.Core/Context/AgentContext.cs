using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using System.Text.RegularExpressions;

namespace AGXIOME.Core.Context;

/// <summary>Agent context: system prompt, instructions, few-shot, introduction message.</summary>
public sealed class AgentContext : IAgentContext
{
    public string? SystemPrompt { get; set; }
    public string? IntroductionMessage { get; set; }
    public List<AgentMessage>? FewShotExamples { get; set; }
    public List<string> Instructions { get; } = new();
    public Dictionary<string, object> AdditionalContext { get; } = new();

    public void AddInstruction(string instruction) => Instructions.Add(instruction);
    public void AddFewShot(AgentMessage input, AgentMessage output) =>
        (FewShotExamples ??= new()).AddRange([input, output]);
}

/// <summary>Default dependency provider. Can be replaced with DI container in production.</summary>
public sealed class DefaultDependencyProvider : IDependencyProvider
{
    private readonly Dictionary<Type, object> _instances = new();
    private readonly Dictionary<Type, Func<object>> _factories = new();

    public T GetDependency<T>() where T : class
    {
        if (_instances.TryGetValue(typeof(T), out var inst)) return (T)inst;
        if (_factories.TryGetValue(typeof(T), out var fac)) return (T)fac();
        throw new InvalidOperationException($"No dependency registered for {typeof(T).Name}");
    }

    public void RegisterDependency<T>(T instance) where T : class => _instances[typeof(T)] = instance;
    public void RegisterDependency<T>(Func<T> factory) where T : class => _factories[typeof(T)] = () => factory()!;
}

/// <summary>Resolves template strings with session state values: {key} → state.Get("key").</summary>
public sealed class DynamicInstructionResolver
{
    private static readonly Regex PlaceholderPattern = new(@"\{(\w+)\}", RegexOptions.Compiled);

    public string Resolve(string template, ISessionState state)
    {
        return PlaceholderPattern.Replace(template, match =>
        {
            var key = match.Groups[1].Value;
            return state.Get<object>(key)?.ToString() ?? match.Value;
        });
    }
}
