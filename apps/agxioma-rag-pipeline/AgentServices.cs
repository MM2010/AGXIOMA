namespace AgxiomaRagPipeline;

// ── Agent configuration ───────────────────────────────────────────────────────

public record AgentSettings
{
    public string Id         { get; init; } = "MY_AGENT_NAME";
    public string? Model     { get; init; }
    public string SystemPrompt { get; init; } =
        """
        You are a precise knowledge-base assistant.
        Answer questions strictly based on the context provided.
        Always cite the source document IDs at the end of your answer.
        If the context is insufficient, say: "The knowledge base does not contain enough information to answer this question."
        """;
    public int    ChunkMaxTokens { get; init; } = 512;
    public int    ChunkOverlap   { get; init; } = 64;
    public int    TopK           { get; init; } = 5;
    public double MinScore       { get; init; } = 0.5;
}

// ── Request / Response models ─────────────────────────────────────────────────

public record IngestRequest(string Path);

public record IngestTextRequest(
    string Id,
    string Content,
    Dictionary<string, string>? Metadata = null
);

public record QueryRequest(
    string Question,
    int? TopK = null,
    double? MinScore = null
);

public record QueryResponse(
    string Question,
    string Answer,
    List<ChunkReference> Sources,
    bool ContextFound
);

public record ChunkReference(
    string DocumentId,
    string Snippet,
    float Score
);
