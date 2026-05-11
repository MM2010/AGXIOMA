using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using Dapper;
using Npgsql;

namespace AGXIOME.Storage.VectorStores;

/// <summary>pgvector-based vector store. Requires PostgreSQL 16+ with pgvector extension.</summary>
public sealed class PgVectorStore : IVectorStore
{
    private readonly string _connectionString;

    public PgVectorStore(string connectionString) => _connectionString = connectionString;

    public async Task UpsertAsync(string docId, Chunk chunk, float[] embedding, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.ExecuteAsync(
            @"INSERT INTO knowledge_chunks (doc_id, chunk_index, content, embedding, metadata_json)
              VALUES (@DocId, @Index, @Content, @Embedding::vector, @Metadata)
              ON CONFLICT (doc_id, chunk_index) DO UPDATE SET content = @Content, embedding = @Embedding::vector",
            new { DocId = docId, Index = chunk.Index, Content = chunk.Text,
                  Embedding = string.Join(",", embedding.Select(f => f.ToString("F8"))),
                  Metadata = "{}" }, commandTimeout: 30);
    }

    public async Task<IReadOnlyList<KnowledgeChunk>> SearchAsync(float[] queryVec, KnowledgeSearchOptions options, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        var vecStr = string.Join(",", queryVec.Select(f => f.ToString("F8")));
        var sql = $@"SELECT doc_id, content, metadata_json, 1 - (embedding <=> @Vec::vector) AS score
                     FROM knowledge_chunks
                     WHERE 1 - (embedding <=> @Vec::vector) >= @MinScore
                     ORDER BY embedding <=> @Vec::vector
                     LIMIT @TopK";

        var results = await conn.QueryAsync<(string DocId, string Content, string Metadata, double Score)>(
            sql, new { Vec = vecStr, MinScore = options.MinScore, TopK = options.TopK }, commandTimeout: 30);

        return results.Select(r => new KnowledgeChunk(r.DocId, r.Content, (float)r.Score)).ToList();
    }

    public async Task DeleteAsync(string docId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.ExecuteAsync("DELETE FROM knowledge_chunks WHERE doc_id = @DocId", new { DocId = docId }, commandTimeout: 30);
    }
}
