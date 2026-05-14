namespace AgxiomaDynamicSkillLab;

// ── Configuration ────────────────────────────────────────────────────────────

public record AgentSettings
{
    public string? Model { get; init; }
    public string DatabasePath { get; init; } = "dynamic_skill_lab.db";
    public string SandboxTempPath { get; init; } = "sandbox_temp";
    public int CompilationTimeoutMs { get; init; } = 15_000;
    public int ExecutionTimeoutMs { get; init; } = 10_000;
}

// ── Tool Lifecycle ───────────────────────────────────────────────────────────

public enum ToolStatus
{
    Proposed,
    Generated,
    Validated,
    Compiled,
    TestsPassed,
    SandboxReady,
    Executed,
    Promoted,
    Failed
}

// ── Dynamic Tool Descriptor ──────────────────────────────────────────────────

public record ToolDescriptor(
    string Id,
    string Name,
    string Description,
    string SourceCode,
    string ManifestJson,
    DateTime CreatedAt,
    ToolStatus Status,
    List<string>? ValidationErrors = null,
    List<string>? CompilationErrors = null,
    List<string>? TestOutput = null
);

// ── Permission Manifest ──────────────────────────────────────────────────────

public record PermissionManifest(
    string Id,
    string Version,
    PermissionRules Permissions
);

public record PermissionRules(
    string Filesystem,
    bool Network,
    List<string> Tools,
    int MaxExecutionMs,
    int MaxMemoryMb
);

// ── Endpoint Request / Response ──────────────────────────────────────────────

public record CapabilityRequest(
    string Task,
    string SessionId = "default"
);

public record ToolProposal(
    string ToolId,
    string ToolName,
    string Description,
    string Rationale,
    List<string> RequiredNamespaces
);

public record ToolGenerateRequest(
    string ToolId,
    string ToolName,
    string Description,
    string TaskContext,
    string SessionId
);

public record ToolRunRequest(
    string ToolId,
    string Input,
    string SessionId
);

public record ToolPromoteRequest(
    string ToolId,
    string Description,
    string SessionId
);

public record PipelineResponse(
    string SessionId,
    string? ToolId,
    string? ToolName,
    ToolStatus Status,
    string Step,
    object? Data,
    List<string>? Errors = null,
    Dictionary<string, TimeSpan>? Timing = null
);

// ── Skill Registry ───────────────────────────────────────────────────────────

public record PersistentSkill(
    string Id,
    string Name,
    string Description,
    string SourceCode,
    string ManifestJson,
    string Hash,
    string Version,
    DateTime CreatedAt,
    DateTime? PromotedAt,
    int UseCount
);

// ── Execution Result ─────────────────────────────────────────────────────────

public record ToolExecutionResult(
    bool Success,
    string Output,
    List<string>? Errors = null
);
