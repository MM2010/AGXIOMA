using AGXIOME.Shared.Models;

namespace AGXIOME.Shared.Interfaces;

// ═══════════════════════════════════════════
// SKILLS SYSTEM
// ═══════════════════════════════════════════

/// <summary>
/// A skill is a self-contained package an Agent can load to extend its capabilities.
/// Follows the Anthropic/Agno Agent Skills specification:
/// <list type="bullet">
///   <item><b>SKILL.md</b>: YAML frontmatter with metadata + Markdown instructions</item>
///   <item><b>scripts/</b>: Optional executable code templates (Python, shell, C#)</item>
///   <item><b>references/</b>: Optional supporting documents (guides, cheatsheets, examples)</item>
/// </list>
/// </summary>
/// <remarks>
/// <para>Skills are immutable after construction. They can derive from a base skill
/// (via <see cref="BaseSkillId"/>) and declare dependencies via <see cref="RequiredSkills"/>.</para>
/// <para>The <see cref="SystemPromptExtension"/> is injected into the agent's system prompt
/// when the skill is applied, providing the LLM with skill-specific instructions.</para>
/// </remarks>
public interface IAgxiomeSkill
{
    /// <summary>Unique skill identifier (e.g., "code-review", "pdf-parser").</summary>
    string Id { get; }

    /// <summary>Human-readable skill name.</summary>
    string Name { get; }

    /// <summary>What the skill does — displayed in skill listings.</summary>
    string Description { get; }

    /// <summary>Semantic version (e.g., "1.2.0").</summary>
    string? Version { get; }

    /// <summary>Category for grouping in the skill registry (e.g., "development", "data").</summary>
    string? Category { get; }

    /// <summary>Optional base skill ID this skill extends/derives from.</summary>
    string? BaseSkillId { get; }

    /// <summary>Tool IDs that this skill contributes to the agent.</summary>
    IReadOnlyList<string> ToolIds { get; }

    /// <summary>Skills that must be loaded before this one can be applied.</summary>
    IReadOnlyList<string> RequiredSkills { get; }

    /// <summary>Text injected into the agent's system prompt when the skill is active.</summary>
    string? SystemPromptExtension { get; }

    /// <summary>Optional knowledge base ID for RAG-backed skill knowledge.</summary>
    string? KnowledgeBaseId { get; }

    /// <summary>Default configuration values for the skill.</summary>
    Dictionary<string, object> DefaultConfig { get; }

    /// <summary>Executable scripts bundled with the skill.</summary>
    IReadOnlyList<SkillScript> Scripts { get; }

    /// <summary>Reference documents bundled with the skill.</summary>
    IReadOnlyList<SkillDocRef> References { get; }
}

/// <summary>An executable script within a skill (Python, shell, C#, etc.).</summary>
/// <param name="Name">Script display name.</param>
/// <param name="Path">Relative path within the skill package.</param>
/// <param name="Language">Script language identifier (python, bash, csharp).</param>
/// <param name="Description">Optional description of what the script does.</param>
public record SkillScript(string Name, string Path, string Language, string? Description = null);

/// <summary>A reference document within a skill (guide, cheatsheet, example).</summary>
/// <param name="Name">Document display name.</param>
/// <param name="Path">Relative path within the skill package.</param>
/// <param name="Description">Optional description of the document's purpose.</param>
public record SkillDocRef(string Name, string Path, string? Description = null);

/// <summary>
/// Manifest for a skill in a remote registry (skills.sh-compatible format).
/// Used for discovery before downloading the full skill package.
/// </summary>
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

/// <summary>
/// A skill package on disk, either from local discovery or downloaded from a remote registry.
/// Contains the parsed metadata and paths to the skill's files.
/// </summary>
/// <param name="Id">Unique skill identifier.</param>
/// <param name="Name">Human-readable name.</param>
/// <param name="Description">What the skill does.</param>
/// <param name="Version">Semantic version string.</param>
/// <param name="Category">Optional grouping category.</param>
/// <param name="Author">Skill author (from YAML frontmatter).</param>
/// <param name="ManifestPath">Absolute path to the SKILL.md manifest file.</param>
/// <param name="ScriptsDir">Optional absolute path to the scripts/ directory.</param>
/// <param name="ReferencesDir">Optional absolute path to the references/ directory.</param>
/// <param name="Tags">Search/discovery tags.</param>
/// <param name="RequiredSkills">Dependencies (must be loaded before this skill).</param>
public record SkillPackage(
    string Id,
    string Name,
    string Description,
    string Version,
    string? Category,
    string? Author,
    string ManifestPath,
    string? ScriptsDir,
    string? ReferencesDir,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> RequiredSkills
);

/// <summary>
/// Central skill orchestrator: manages multiple loaders, resolves dependencies,
/// and binds skills to agents.
/// </summary>
/// <remarks>
/// <para><b>Lifecycle</b>: Register loaders → Discover skills → Apply to agents.
/// Skills can be registered from local packages or downloaded from remote registries.</para>
/// <para><b>Dependency resolution</b>: <see cref="ApplySkillToAgent"/> automatically
/// loads required skills if they are registered but not yet applied.</para>
/// </remarks>
public interface ISkillManager
{
    /// <summary>Registers a pre-built skill instance.</summary>
    void RegisterSkill(IAgxiomeSkill skill);

    /// <summary>Loads and registers a skill from an on-disk package.</summary>
    void RegisterFromPackage(SkillPackage package);

    /// <summary>Removes a skill from the registry. Does not unapply from agents.</summary>
    void UnregisterSkill(string skillId);

    /// <summary>Looks up a registered skill by ID. Returns null if not found.</summary>
    IAgxiomeSkill? GetSkill(string skillId);

    /// <summary>Lists all registered skills, optionally filtered by category.</summary>
    IReadOnlyList<IAgxiomeSkill> ListSkills(string? category = null);

    /// <summary>Downloads a skill from a remote registry (skills.sh format) and registers it.</summary>
    Task<IAgxiomeSkill> DownloadSkillAsync(string skillId, string registryUrl, CancellationToken ct = default);

    /// <summary>Lists skills available on a remote registry without downloading them.</summary>
    Task<IReadOnlyList<SkillManifest>> ListRemoteAsync(string registryUrl, CancellationToken ct = default);

    /// <summary>Binds a skill to an agent: injects prompt extension, registers tools, applies config.</summary>
    void ApplySkillToAgent(IAgxiomeAgent agent, string skillId);

    /// <summary>Removes a skill from an agent: removes prompt extension, unregisters tools.</summary>
    void RemoveSkillFromAgent(IAgxiomeAgent agent, string skillId);

    /// <summary>Returns the skill IDs currently applied to the given agent.</summary>
    IReadOnlyList<string> GetAgentSkillIds(string agentId);

    /// <summary>Registers a skill loader for discovering skills from a source.</summary>
    void AddLoader(ISkillLoader loader);
}

/// <summary>
/// Discovers skills from a specific source (local directory, remote registry, custom provider).
/// </summary>
public interface ISkillLoader
{
    /// <summary>Loader name for observability (e.g., "local-dir", "github-registry").</summary>
    string Name { get; }

    /// <summary>Discovers all available skills from this source.</summary>
    Task<IReadOnlyList<SkillPackage>> DiscoverAsync(CancellationToken ct = default);

    /// <summary>Loads a specific skill by ID from this source.</summary>
    Task<SkillPackage?> LoadAsync(string skillId, CancellationToken ct = default);
}

// ═══════════════════════════════════════════
// HITL COMPLETION SOURCE
// ═══════════════════════════════════════════

/// <summary>
/// Decoupled notification contract for HITL resolution events.
/// Defined in Shared so AGXIOME.Core (<c>HITLManager</c>) can call it without
/// referencing AGXIOME.Orchestration. Implemented by completion notifier providers
/// (InMemory, SignalR, XLink).
/// </summary>
public interface IHitlCompletionSource
{
    /// <summary>
    /// Notifies the orchestration layer that a paused HITL requirement has been resolved.
    /// The orchestrator uses this to publish a synthetic <c>TaskResult</c> so the polling
    /// loop can continue execution.
    /// </summary>
    /// <param name="taskId">The orchestration task that was paused.</param>
    /// <param name="planId">The parent orchestration plan.</param>
    /// <param name="requirementId">The HITL requirement that was resolved.</param>
    /// <param name="approved">Whether the requirement was approved or rejected.</param>
    /// <param name="ct">Cancellation token.</param>
    Task NotifyResolvedAsync(Guid taskId, Guid planId, string requirementId, bool approved,
        CancellationToken ct = default);
}

// ═══════════════════════════════════════════
// AUDIT LOGGER
// ═══════════════════════════════════════════

/// <summary>
/// Writes and queries pseudonymized audit events for compliance and debugging.
/// All personally identifiable information is hashed or redacted before storage.
/// </summary>
public interface IAuditLogger
{
    /// <summary>Records a single audit event.</summary>
    Task LogAsync(AuditEvent evt, CancellationToken ct = default);

    /// <summary>Queries audit events by time range, agent, action type, etc.</summary>
    Task<IReadOnlyList<AuditEvent>> QueryAsync(AuditQuery query, CancellationToken ct = default);
}

// ═══════════════════════════════════════════
// CRYPTOGRAPHY
// ═══════════════════════════════════════════

/// <summary>
/// ECC-based cryptographic operations for message signing, verification, and HMAC integrity.
/// Uses ECDSA for signatures and HMAC-SHA256 for data integrity checks.
/// </summary>
/// <remarks>
/// <para><b>Key lifecycle</b>: Keys should be rotated periodically. Use the <c>kid</c> (key ID)
/// header in signed payloads to identify which key was used.</para>
/// </remarks>
public interface IAgxiomeCrypto
{
    /// <summary>Generates a new ECDSA key pair (P-256 curve).</summary>
    /// <returns>A tuple of (privateKey, publicKey) as raw byte arrays.</returns>
    (byte[] PrivateKey, byte[] PublicKey) GenerateKeyPair();

    /// <summary>Signs data with the given private key using ECDSA.</summary>
    byte[] Sign(byte[] data, byte[] privateKey);

    /// <summary>Verifies an ECDSA signature against the original data and public key.</summary>
    bool Verify(byte[] data, byte[] signature, byte[] publicKey);

    /// <summary>Computes an HMAC-SHA256 over the data with the given key.</summary>
    string ComputeHMAC(byte[] data, byte[] key);

    /// <summary>Verifies an HMAC-SHA256 against the original data and key.</summary>
    bool VerifyHMAC(byte[] data, string hmac, byte[] key);
}

// ═══════════════════════════════════════════
// X-LINK TRANSPORT
// ═══════════════════════════════════════════

/// <summary>
/// X-Link transport protocol: sends typed messages between agents with optional compression,
/// checksum verification, and routing.
/// </summary>
/// <remarks>
/// <para><b>Patterns</b>: Supports fire-and-forget (<see cref="SendAsync{T}"/>),
/// publish-subscribe (<see cref="PublishAsync{T}"/> + <see cref="Subscribe{T}"/>),
/// and request-response (<see cref="RequestAsync{T}"/>).</para>
/// <para><b>Compression</b>: Payloads larger than 1KB are automatically compressed with Gzip
/// when <see cref="XLinkOptions.Compression"/> is set. Subscribers receive decompressed values
/// transparently.</para>
/// </remarks>
public interface IXLinkRouter
{
    /// <summary>Sends a message to a specific target. Fire-and-forget unless a subscriber is registered.</summary>
    /// <typeparam name="T">The payload type.</typeparam>
    /// <param name="target">Target identifier (topic/channel/agent ID).</param>
    /// <param name="payload">The message payload.</param>
    /// <param name="options">Optional routing/compression/security options.</param>
    Task SendAsync<T>(string target, T payload, XLinkOptions? options = null);

    /// <summary>Publishes a message to a topic. Alias for <see cref="SendAsync{T}"/>.</summary>
    Task PublishAsync<T>(string topic, T payload, XLinkOptions? options = null);

    /// <summary>Sends a request and waits for a response. Not yet implemented — throws <see cref="NotImplementedException"/>.</summary>
    /// <exception cref="NotImplementedException">Always thrown in the current implementation.</exception>
    Task<T> RequestAsync<T>(string target, object payload, TimeSpan? timeout = null);

    /// <summary>Subscribes a handler to a topic. The handler receives deserialized payloads.</summary>
    void Subscribe<T>(string topic, Func<T, Task> handler);
}

// ═══════════════════════════════════════════
// AGENT CONTEXT
// ═══════════════════════════════════════════

/// <summary>
/// Mutable configuration context passed through the agent pipeline (middleware, hooks, guardrails).
/// Provides access to system prompt, instructions, few-shot examples, and custom context data.
/// </summary>
public interface IAgentContext
{
    /// <summary>Gets or sets the system prompt for the current run.</summary>
    string? SystemPrompt { get; set; }

    /// <summary>Gets or sets the introduction message shown before the conversation.</summary>
    string? IntroductionMessage { get; set; }

    /// <summary>Gets or sets few-shot examples for in-context learning.</summary>
    List<AgentMessage>? FewShotExamples { get; set; }

    /// <summary>Gets the list of instructions added via <see cref="AddInstruction"/>.</summary>
    List<string> Instructions { get; }

    /// <summary>Arbitrary key-value pairs for middleware/hook communication.</summary>
    Dictionary<string, object> AdditionalContext { get; }

    /// <summary>Adds an instruction to the agent's prompt.</summary>
    void AddInstruction(string instruction);

    /// <summary>Adds a few-shot example (input-output pair).</summary>
    void AddFewShot(AgentMessage input, AgentMessage output);
}
