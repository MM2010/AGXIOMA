namespace AGXIOME.Shared.Models;

// ─────────────────────────────────────────────────────────────────────────────
// Response cache / token counting / CLI / compatibility
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Caching strategy for LLM response deduplication.</summary>
public enum CacheStrategy
{
    None = 0,
    InMemory = 1,
    Redis = 2,
    Hybrid = 3,
}

/// <summary>Tokenizer algorithm used for accurate prompt token estimation.</summary>
public enum TokenizerAlgorithm
{
    Cl100kBase = 0,   // GPT-4 / GPT-3.5
    O200kBase = 1,    // o1/o3/gpt-4o
    Llama3 = 2,
    Llama2 = 3,
    Mistral = 4,
    Gemini = 5,
    Generic = 6,
}

/// <summary>Top-level CLI command group.</summary>
public enum CliCommand
{
    Run = 0,
    List = 1,
    Session = 2,
    Admin = 3,
    Config = 4,
}

/// <summary>Output format for CLI commands.</summary>
public enum CliOutputFormat
{
    Plain = 0,
    Json = 1,
    Table = 2,
}

/// <summary>Compatibility lifecycle status of a specific model version.</summary>
public enum ModelCompatibilityStatus
{
    Supported = 0,
    Deprecated = 1,
    Removed = 2,
    Preview = 3,
}

/// <summary>Multi-framework bridge target for interop with external agent frameworks.</summary>
public enum BridgeFramework
{
    LangChain = 0,
    SemanticKernel = 1,
    ClaudeSDK = 2,
}
