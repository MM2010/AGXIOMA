# P11 — Context Engineering

## File: `AgentContext.cs`

Namespace: `AGXIOME.Core.Context`

## Classi

### `AgentContext : IAgentContext`

```csharp
public class AgentContext : IAgentContext
{
    public string? SystemPrompt { get; set; }
    public string? IntroductionMessage { get; set; }
    public List<AgentMessage>? FewShotExamples { get; set; }
    public List<string> Instructions { get; } = new();
    public Dictionary<string, object> AdditionalContext { get; } = new();

    public void AddInstruction(string i) => Instructions.Add(i);
    public void AddInstruction(Func<Session, string> dynamic)
        => Instructions.Add(dynamic(new Session("", "", "", SessionType.Agent, null, null, null, default))); // dynamic
    public void SetSystemMessage(string m) => SystemPrompt = m;
    public void SetIntroduction(string m) => IntroductionMessage = m;
    public void AddFewShot(AgentMessage input, AgentMessage output)
        => FewShotExamples ??= new() { input, output };
}
```

### `IDependencyProvider`

```csharp
public interface IDependencyProvider
{
    T GetDependency<T>() where T : class;
    void RegisterDependency<T>(T instance) where T : class;
    void RegisterDependency<T>(Func<T> factory) where T : class;
}

public class DefaultDependencyProvider : IDependencyProvider
{
    private readonly Dictionary<Type, object> _instances = new();
    private readonly Dictionary<Type, Func<object>> _factories = new();

    public T GetDependency<T>() where T : class
    {
        if (_instances.TryGetValue(typeof(T), out var inst)) return (T)inst;
        if (_factories.TryGetValue(typeof(T), out var fac)) return (T)fac();
        throw new InvalidOperationException($"No dependency registered for {typeof(T).Name}");
    }

    public void RegisterDependency<T>(T instance) where T : class
        => _instances[typeof(T)] = instance;

    public void RegisterDependency<T>(Func<T> factory) where T : class
        => _factories[typeof(T)] = () => factory()!;
}
```

## Dynamic instructions (Agno compat)

```csharp
// Istruzioni che referenziano session_state a runtime:
// "Current state (shopping list) is: {shopping_list}"
// → Template string con placeholder risolti via SessionState.Get()

public class DynamicInstructionResolver
{
    public string Resolve(string template, ISessionState state)
    {
        return Regex.Replace(template, @"\{(\w+)\}", match =>
        {
            var key = match.Groups[1].Value;
            return state.Get<object>(key)?.ToString() ?? match.Value;
        });
    }
}
```

## Test

| Test | Descrizione |
|------|------------|
| Instructions rendering | Template risolto |
| Dependency injection | Dipendenza disponibile in tool |
| Few-shot included | Esempi nel prompt |
| Introduction message | Primo messaggio custom |
