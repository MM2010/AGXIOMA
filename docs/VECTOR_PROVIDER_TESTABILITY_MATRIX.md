# AGXIOME Vector Provider Testability Matrix

This matrix separates two different claims that must never be mixed in reports:

- `AGXIOME boundary covered`: request validation, mapping, fail-fast behavior, retries, parsing, and error translation are covered.
- `Vendor runtime truly covered`: the test exercises a faithful local runtime for the provider's real behavior.

| Provider | Current coverage level | Docker fidelity | Estimated effort | Priority | Residual AGXIOME risk |
|---|---|---|---|---|---|
| Qdrant Cloud/SelfHosted | Boundary covered + vendor runtime truly covered | High | Done | High | Low |
| Redis Stack / RediSearch | Boundary covered + vendor runtime truly covered | High | Done | High | Low |
| ElasticsearchDense | Boundary covered + vendor runtime truly covered when container major matches client major | High | Done | High | Low |
| MeilisearchVector | Boundary covered + vendor runtime truly covered | High | Done | High | Low |
| PgVector | Boundary covered + vendor runtime truly covered | High | Done | High | Low |
| SupabasePgVector | Boundary covered + pgvector runtime covered, cloud control-plane not covered | Medium | Done | High | Low-Medium |
| NeonPgVector | Boundary covered + pgvector runtime covered, cloud control-plane not covered | Medium | Done | High | Low-Medium |
| TimescalePgVector | Boundary covered + pgvector runtime covered, extension-specific cloud features not covered | Medium | Done | High | Low-Medium |
| Pinecone | Boundary covered via deterministic HTTP seams | Low | 0.5-1 day | Medium | Medium |
| Weaviate Cloud/SelfHosted | Boundary covered via shared REST seams; real runtime only if later added with container | Medium | 1-2 days | Medium | Medium |
| Milvus Cloud/SelfHosted | Boundary covered via shared REST seams; real runtime can be added later but costs more infra | Medium | 1-2 days seam / 2-4 days live | Medium | Medium |
| Chroma Cloud/SelfHosted | Boundary covered via shared REST seams; truthful runtime possible later | Medium | 1-2 days seam / 2-3 days live | Medium | Medium |
| Vald | Boundary covered via deterministic HTTP seams | Low | 1 day | Medium | Medium |
| Vespa | Boundary covered via deterministic HTTP seams | Low | 1 day | Medium | Medium |
| LanceDB | Boundary covered via deterministic HTTP seams | Low | 1 day | Medium | Medium |
| ScaNN | Boundary covered via deterministic HTTP seams | Low | 1 day | Medium | Medium |
| MongoDBAtlas | Boundary covered via SDK seams; vendor vector runtime not truly covered locally | Low | 1-2 days | Medium | Medium-High |
| CosmosDBVCore | Boundary coverage possible, vendor runtime not honestly coverable with generic Docker | Very low | 2-3 days seam only / real coverage needs cloud env | High | High |
| CockroachPgVector | Boundary coverage possible, truthful runtime depends on proven semantic parity with pgvector | Low-Medium | 2-3 days investigation | High | High |
| SingleStoreVector | Boundary/runtime coverage possible for AGXIOME's current SQL+JSON implementation; not true native-vector coverage | Medium | 1-2 days | Medium | Medium |
| RocksDBHNSW | Boundary/runtime coverage possible only with native RocksDB runtime, not a server container | Very low | 1-3 days native setup | High | High |
| SQLServerVector | Boundary coverage possible; truthful runtime requires SQL Server version with VECTOR feature enabled | Very low | 2-4 days env validation | High | High |
| SqliteVec | Local truthful coverage without Docker | High | 0.5 day | Medium | Low |
| DuckDBVector | Local truthful coverage without Docker if extension path is stable | Medium | 0.5-1 day | Medium | Low-Medium |
| HNSWLib / FAISS / Annoy | Boundary/runtime coverage is local-process based, not Docker-centric | Medium | 1-2 days each | Low-Medium | Medium |

## Investment order

1. Finish seam-hardening for all shared REST providers because the cost is low and the AGXIOME signal is high.
2. Add Docker only where the runtime is faithful: Qdrant, Redis Stack, Elasticsearch, Meilisearch, pgvector, and optionally SingleStore current-contract coverage.
3. Report `boundary covered` and `vendor runtime truly covered` as separate columns in CI and quality dashboards.
4. Keep Cosmos vCore, SQL Server VECTOR, RocksDB native, and Cockroach parity validation in a separate backlog because they require environment truth, not just more tests.