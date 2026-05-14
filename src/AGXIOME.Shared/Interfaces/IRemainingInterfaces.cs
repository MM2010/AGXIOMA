using AGXIOME.Shared.Models;

namespace AGXIOME.Shared.Interfaces;

// ═══════════════════ SKILLS SYSTEM ═══════════════════

/// <summary>
/// A skill is a self-contained package an Agent can use to extend its capabilities.
/// Follows Agno/Anthropic Agent Skills specification:
/// - SKILL.md: instructions with YAML frontmatter
/// - scripts/: optional executable code templates
/// - references/: optional supporting docs
/// </summary>
public interface IAgxiomeSkill
{
    string Id { get; }
    string Name { get; }
    string Description { get; }
    string? Version { get; }
    string? Category { get; }
    string? BaseSkillId { get; }
    IReadOnlyList<string> ToolIds { get; }
    IReadOnlyList<string> RequiredSkills { get; }
    string? SystemPromptExtension { get; }
    string? KnowledgeBaseId { get; }
    Dictionary<string, object> DefaultConfig { get; }
    IReadOnlyList<SkillScript> Scripts { get; }
    IReadOnlyList<SkillDocRef> References { get; }
}

/// <summary>An executable script within a skill (Python, shell, C#).</summary>
public record SkillScript(string Name, string Path, string Language, string? Description = null);

/// <summary>A reference document within a skill (guide, cheatsheet, example).</summary>
public record SkillDocRef(string Name, string Path, string? Description = null);

/// <summary>Manifest for remote skill registry (skills.sh style).</summary>
public record SkillManifest(
    string Id,
    string Name,
    string Description,
    string Version,
    string? Category,
    string? Author,
    Uri? DownloadUrl,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> RequiredSkills
);

/// <summary>Skill package on disk (from LocalSkills or downloaded).</summary>
public record SkillPackage(
    string Id,
    string Name,
    string Description,
    string Version,
    string? Category,
    string? Author,
    string ManifestPath,       // Path to SKILL.md
    string? ScriptsDir,        // Optional: scripts/ dir
    string? ReferencesDir,     // Optional: references/ dir
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> RequiredSkills
);

/// <summary>Skill orchestrator: manages multiple loaders and agent-to-skill binding.</summary>
public interface ISkillManager
{
    void RegisterSkill(IAgxiomeSkill skill);
    void RegisterFromPackage(SkillPackage package);
    void UnregisterSkill(string skillId);
    IAgxiomeSkill? GetSkill(string skillId);
    IReadOnlyList<IAgxiomeSkill> ListSkills(string? category = null);

    // Remote registry (skills.sh style)
    Task<IAgxiomeSkill> DownloadSkillAsync(string skillId, string registryUrl, CancellationToken ct = default);
    Task<IReadOnlyList<SkillManifest>> ListRemoteAsync(string registryUrl, CancellationToken ct = default);

    // Agent binding
    void ApplySkillToAgent(IAgxiomeAgent agent, string skillId);
    void RemoveSkillFromAgent(IAgxiomeAgent agent, string skillId);
    IReadOnlyList<string> GetAgentSkillIds(string agentId);

    // Loaders
    void AddLoader(ISkillLoader loader);
}

/// <summary>Skill loader: discovers skills from a source (local dir, remote, custom).</summary>
public interface ISkillLoader
{
    string Name { get; }
    Task<IReadOnlyList<SkillPackage>> DiscoverAsync(CancellationToken ct = default);
    Task<SkillPackage?> LoadAsync(string skillId, CancellationToken ct = default);
}

// IChunker  → see IProviderInterfaces.cs (full typed contract with ChunkerType enum)
// IEmbedder → see IProviderInterfaces.cs (full typed contract with EmbedderType enum)

/// <summary>
/// Decoupled notification contract for HITL resolution events.
/// Defined in Shared so AGXIOME.Core (HITLManager) can call it without referencing AGXIOME.Orchestration.
/// Implemented by ICompletionNotifier providers (InMemory, SignalR, XLink).
/// </summary>
public interface IHitlCompletionSource
{
    /// <summary>
    /// Notifies the orchestration layer that a paused HITL requirement has been resolved.
    /// The orchestrator uses this to publish a synthetic TaskResult so the polling loop can continue.
    /// </summary>
    Task NotifyResolvedAsync(Guid taskId, Guid planId, string requirementId, bool approved,
        CancellationToken ct = default);
}

public interface IAuditLogger
{
    Task LogAsync(AuditEvent evt, CancellationToken ct = default);
    Task<IReadOnlyList<AuditEvent>> QueryAsync(AuditQuery query, CancellationToken ct = default);
}

public interface IAgxiomeCrypto
{
    (byte[] PrivateKey, byte[] PublicKey) GenerateKeyPair();
    byte[] Sign(byte[] data, byte[] privateKey);
    bool Verify(byte[] data, byte[] signature, byte[] publicKey);
    string ComputeHMAC(byte[] data, byte[] key);
    bool VerifyHMAC(byte[] data, string hmac, byte[] key);
}

public interface IXLinkRouter
{
    Task SendAsync<T>(string target, T payload, XLinkOptions? options = null);
    Task PublishAsync<T>(string topic, T payload, XLinkOptions? options = null);
    Task<T> RequestAsync<T>(string target, object payload, TimeSpan? timeout = null);
    void Subscribe<T>(string topic, Func<T, Task> handler);
}

// IVectorStore → see IProviderInterfaces.cs (full typed contract with VectorDbProviderType enum)

public interface IAgentContext
{
    string? SystemPrompt { get; set; }
    string? IntroductionMessage { get; set; }
    List<AgentMessage>? FewShotExamples { get; set; }
    List<string> Instructions { get; }
    Dictionary<string, object> AdditionalContext { get; }
    void AddInstruction(string instruction);
    void AddFewShot(AgentMessage input, AgentMessage output);
}
