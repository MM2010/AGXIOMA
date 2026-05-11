using AGXIOME.Shared.Models;

namespace AGXIOME.Shared.Interfaces;

public interface ISkillManager
{
    void RegisterSkill(IAgxiomeSkill skill);
    void UnregisterSkill(string skillId);
    IAgxiomeSkill? GetSkill(string skillId);
    IReadOnlyList<IAgxiomeSkill> ListSkills(string? category = null);
    Task<IAgxiomeSkill> DownloadSkillAsync(string skillId, string source, CancellationToken ct = default);
    void ApplySkillToAgent(IAgxiomeAgent agent, string skillId);
    void RemoveSkillFromAgent(IAgxiomeAgent agent, string skillId);
}

public interface IAgxiomeSkill
{
    string Id { get; }
    string Name { get; }
    string Description { get; }
    string? Version { get; }
    string? Category { get; }
    string? BaseSkillId { get; }
    IReadOnlyList<string> ToolIds { get; }
    string? SystemPromptExtension { get; }
    string? KnowledgeBaseId { get; }
    Dictionary<string, object> DefaultConfig { get; }
}

public interface IChunker
{
    IReadOnlyList<Chunk> Chunk(string content, DocumentType type);
}

public interface IEmbedder
{
    int Dimensions { get; }
    Task<float[]> EmbedAsync(string text, CancellationToken ct = default);
    Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default);
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

/// <summary>Vector store for RAG: upsert embeddings, ANN search, delete.</summary>
public interface IVectorStore
{
    Task UpsertAsync(string docId, Chunk chunk, float[] embedding, CancellationToken ct = default);
    Task<IReadOnlyList<KnowledgeChunk>> SearchAsync(float[] queryVec, KnowledgeSearchOptions options, CancellationToken ct = default);
    Task DeleteAsync(string docId, CancellationToken ct = default);
}

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
