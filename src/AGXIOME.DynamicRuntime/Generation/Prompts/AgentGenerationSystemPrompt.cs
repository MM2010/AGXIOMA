using AGXIOME.DynamicRuntime.Templates;

namespace AGXIOME.DynamicRuntime.Generation.Prompts;

/// <summary>
/// System prompt per la generazione di agenti C# via LLM.
/// Include: interfaccia, attributo, template, 3 esempi few-shot,
/// allowlist/blocklist namespace, regola output.
/// </summary>
public static class AgentGenerationSystemPrompt
{
    private static readonly string[] DefaultBlockedNamespaces =
    [
        "System.IO", "System.Reflection", "System.Diagnostics.Process",
        "System.Runtime.InteropServices", "System.Security", "unsafe", "DllImport"
    ];

    private static readonly string[] DefaultAllowedNamespaces =
    [
        "System", "System.Collections.Generic", "System.Linq",
        "System.Text", "System.Text.Json", "System.Threading", "System.Threading.Tasks",
        "System.Net.Http",
        "AGXIOME.DynamicRuntime.Attributes", "AGXIOME.DynamicRuntime.Interfaces",
        "AGXIOME.DynamicRuntime.Bridge" // AgentServices.AskAsync(), .GetAsync(), .PostJsonAsync()
    ];

    public static string Build(string[]? additionalAllowed = null)
    {
        var allowed = additionalAllowed is null
            ? DefaultAllowedNamespaces
            : [.. DefaultAllowedNamespaces, .. additionalAllowed];

        var allowedList = string.Join("\n", allowed.Select(n => $"- {n}"));
        var blockedList = string.Join("\n", DefaultBlockedNamespaces.Select(n => $"- {n}"));
        var examples = AgentTemplateExamples.AllExamples;

        // Use string concatenation to avoid raw string literal issues with C# braces
        return
            "You are an expert C# developer. Your task is to generate a single C# class that implements the AGXIOMA dynamic agent interface.\n\n" +
            "## Required Interface\n\n" +
            "```csharp\n" +
            "namespace AGXIOME.DynamicRuntime.Interfaces;\n" +
            "public interface ISimpleDynamicAgent\n" +
            "{\n" +
            "    string AgentId { get; }\n" +
            "    string Description { get; }\n" +
            "    Task<string> RunAsync(string input, CancellationToken ct = default);\n" +
            "}\n" +
            "```\n\n" +
            "## Required Attribute\n\n" +
            "```csharp\n" +
            "[AgxiomeAgent(\"agent-id-kebab-case\", \"Human readable description\")]\n" +
            "```\n\n" +
            "## Allowed Namespaces (using directives)\n" +
            allowedList + "\n\n" +
            "## Blocked Namespaces (NEVER use)\n" +
            blockedList + "\n\n" +
            "## Examples\n\n" +
            examples + "\n\n" +
            "## AgentServices — built-in capabilities\n" +
            "Use `AgentServices` (static, namespace `AGXIOME.DynamicRuntime.Bridge`) for AI and HTTP:\n" +
            "- `await AgentServices.AskAsync(prompt)` → calls the LLM, returns string\n" +
            "- `await AgentServices.AskAsync(prompt, systemPrompt, temperature, maxTokens, ct)` → full overload\n" +
            "- `await AgentServices.GetAsync(url, ct)` → HTTP GET, returns body string\n" +
            "- `await AgentServices.PostJsonAsync(url, payload, ct)` → HTTP POST JSON, returns body string\n\n" +
            "## Rules\n" +
            "1. Respond with ONLY a single ```csharp code block. No explanations, no markdown outside the block.\n" +
            "2. The class MUST have [AgxiomeAgent(\"id\", \"description\")] attribute.\n" +
            "3. The class MUST implement ISimpleDynamicAgent.\n" +
            "4. The AgentId property MUST return the same id string as the attribute.\n" +
            "5. NEVER use blocked namespaces. NEVER use unsafe code.\n" +
            "6. The agent-id in the attribute must be kebab-case (e.g. \"my-agent\").\n" +
            "7. Do NOT add constructor parameters — the class must be instantiable with Activator.CreateInstance().\n";
    }
}
