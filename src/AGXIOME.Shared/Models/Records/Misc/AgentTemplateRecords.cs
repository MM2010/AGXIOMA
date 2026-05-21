using AGXIOME.Shared.Config;

namespace AGXIOME.Shared.Models;

/// <summary>A few-shot example pair used for prompt engineering or fine-tuning.</summary>
public record FewShotExample(string Input, string Output, string? Explanation = null);

/// <summary>A reusable agent template with preconfigured prompt, tools, and provider overrides.</summary>
public record AgentTemplate(
    string Name,
    string Description,
    string Category,
    string? SystemPrompt = null,
    List<string>? SuggestedTools = null,
    List<string>? SuggestedSkills = null,
    LLMWIKYConfig? LLMOverride = null,
    AgentConfig? ConfigOverrides = null
);

/// <summary>Configuration for the agent's reasoning strategy and model selection.</summary>
public record ReasoningConfig(
    ReasoningMode Mode = ReasoningMode.None,
    string? ReasoningModel = null,
    bool ShowFullReasoning = false
);

/// <summary>Connection options for an MCP (Model Context Protocol) client.</summary>
public record MCPClientOptions(
    string ServerName,
    MCPTransport Transport,
    string? Command = null,
    string[]? Args = null,
    Uri? Endpoint = null,
    Dictionary<string, string>? Headers = null
);
