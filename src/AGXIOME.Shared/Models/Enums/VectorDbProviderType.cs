namespace AGXIOME.Shared.Models;

// ─────────────────────────────────────────────────────────────────────────────
// Vector DB providers — 30 providers
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Typed vector database provider discriminator covering cloud and self-hosted stores.</summary>
public enum VectorDbProviderType
{
    // Existing 1
    PgVector = 0,

    // New 30 — VD-01..VD-30
    Pinecone = 10,
    WeaviateCloud = 11,
    QdrantCloud = 12,
    MilvusCloud = 13,
    ChromaDB = 14,
    SupabasePgVector = 15,
    NeonPgVector = 16,
    TimescalePgVector = 17,
    CockroachPgVector = 18,
    SingleStoreVector = 19,
    QdrantSelfHosted = 20,
    WeaviateSelfHosted = 21,
    MilvusSelfHosted = 22,
    ChromaSelfHosted = 23,
    ElasticsearchDense = 24,
    MeilisearchVector = 25,
    Vald = 26,
    Vespa = 27,
    SqliteVec = 28,
    LanceDB = 29,
    DuckDBVector = 30,
    RedisStack = 31,
    MongoDBAtlas = 32,
    CosmosDBVCore = 33,
    HNSWLib = 34,
    FAISS = 35,
    Annoy = 36,
    ScaNN = 37,
    RocksDBHNSW = 38,
    SQLServerVector = 39,
}
