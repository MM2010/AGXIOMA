namespace AGXIOME.Shared.Models;

// ─────────────────────────────────────────────────────────────────────────────
// Storage / DB providers — 25 providers
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Typed storage/database provider discriminator covering SQL, NoSQL, KV, and search backends.</summary>
public enum StorageProviderType
{
    // Existing 5
    PostgreSQL = 0,
    MySQL = 1,
    MariaDB = 2,
    MongoDB = 3,
    RedisCache = 4,

    // New 25 — DB-01..DB-25
    SQLite = 10,
    SQLServer = 11,
    SQLServerLocalDB = 12,
    Neon = 13,
    Supabase = 14,
    SingleStore = 15,
    CockroachDB = 16,
    TimescaleDB = 17,
    DynamoDB = 18,
    CosmosDB = 19,
    Firestore = 20,
    SurrealDB = 21,
    RavenDB = 22,
    Couchbase = 23,
    RedisSession = 24,
    Memcached = 25,
    Hazelcast = 26,
    Etcd = 27,
    ClickHouse = 28,
    InfluxDB = 29,
    QuestDB = 30,
    DuckDB = 31,
    Elasticsearch = 32,
    Meilisearch = 33,
    JsonFile = 34,
}
