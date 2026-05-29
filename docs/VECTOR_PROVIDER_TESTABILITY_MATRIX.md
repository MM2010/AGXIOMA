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
| Vespa | Boundary covered + vendor runtime truly covered | High | Done | High | Low |
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

## Managed Cloud Runtime Validation

### VD-23 MongoDB Atlas

Local status: boundary-covered only. The local slice `MongoDbAtlasVectorStoreTests` passed, but Atlas `$vectorSearch` is not truthfully covered without a managed Atlas cluster and a real vector index.

Runtime validation steps:

1. Provision a MongoDB Atlas cluster with Vector Search enabled.
2. Create a collection with documents using the `vector` field and create the `vector_index` search index expected by `MongoDbAtlasVectorStore`.
3. Supply the Atlas connection string and target database through environment/config, never hardcoded.
4. Run a focused live slice for `EnsureCollection`, `Upsert/Get`, `Search`, and `Delete` against the real Atlas endpoint.
5. Remove validation documents after the run and record index name, dimensions, and endpoint constraints in repo docs.

Acceptance bar:

- Upsert/Get/Search/Delete pass against Atlas with the real `vector_index`.
- Search returns real `vectorSearchScore` values from Atlas, not seam-generated values.
- No local Docker runtime is counted as vendor-runtime coverage for this provider.

### VD-24 Cosmos DB vCore

Local status: constructor/boundary-only. The focused local slice passed with 4 live-vector tests intentionally skipped because Cosmos DB vCore vector search needs a real managed account.

Runtime validation steps:

1. Provision an Azure Cosmos DB vCore account with vector search enabled.
2. Create the target database and container with the production-compatible indexing/vector policy expected by `CosmosDbVCoreStore`.
3. Supply endpoint, database id, and credentials via environment/config.
4. Unskip or duplicate the focused CRUD/search tests into a live-only slice gated by explicit env vars.
5. Run the live slice against the managed account, then delete validation records and document throughput/cost constraints.

Acceptance bar:

- The currently skipped CRUD/search tests pass against the real vCore account.
- Vector search behavior is verified on the managed service, not inferred from the Mongo-compatible surface alone.
- Cleanup is automatic or scripted so cloud validation does not leave residual cost-bearing data behind.

### Next Local Frontier

After `VD-23` and `VD-24`, the next local-only open frontier is `VD-28 ScaNN`.
Current status: the focused seam slice `ScaNNStoreTests` passes locally, so the remaining gap is truthful vendor-runtime coverage, not AGXIOME boundary behavior.

### Native And Version-Gated Runtime Validation

#### VD-28 ScaNN

Local status: seam-covered only. `ScaNNStoreTests` passed against deterministic HTTP handlers, but the implementation targets a custom HTTP wrapper around ScaNN, not a canonical vendor runtime that can be truthfully proven here with generic Docker.

Acceptance bar:

- Validate against the actual ScaNN-serving wrapper that production intends to use.
- Prove collection lifecycle, upsert, get, delete, and search behavior on that wrapper, not only on local seams.
- Record the wrapper contract as the authoritative runtime target if no standard upstream server exists.

#### VD-29 RocksDB + HNSW

Local status: vendor runtime now truly covered locally. After adding `RocksDbNative 6.2.2` alongside `RocksDbSharp`, the focused isolated slice `RocksDbHnswStoreTests` passed `7/7` against the real embedded runtime on this workstation.

Acceptance bar:

- Native runtime materializes `rocksdb.dll` into the isolated test output.
- `RocksDbHnswStoreTests` execute end-to-end without skips.
- CRUD and search pass on the real embedded runtime, not only on seams.

#### VD-30 SQL Server + vector

Local status: boundary-only plus skipped runtime tests. Truthful runtime coverage requires a SQL Server build with the VECTOR feature enabled; current local slices confirm that this environment does not provide that runtime.

Acceptance bar:

- Provision SQL Server 2025-or-compatible runtime with VECTOR support actually enabled.
- Execute the currently skipped CRUD/search/list tests against that runtime.
- Record server version/feature flag prerequisites so future reruns can distinguish environment gaps from provider defects.

## Executable Live Hooks

The focused F5 slice now exposes discovery-time live hooks instead of hardcoded placeholder skips. If the required environment variables are missing, the tests are reported as skipped with the missing prerequisites named explicitly.

Environment variables:

- `AGXIOME_MONGODB_ATLAS_CONNECTION_STRING`
- `AGXIOME_MONGODB_ATLAS_DATABASE`
- `AGXIOME_MONGODB_ATLAS_COLLECTION`
- `AGXIOME_COSMOS_VCORE_CONNECTION_STRING`
- `AGXIOME_COSMOS_VCORE_DATABASE`
- `AGXIOME_COSMOS_VCORE_COLLECTION`
- `AGXIOME_SQLSERVER_VECTOR_CONNECTION_STRING`
- `AGXIOME_SCANN_ENDPOINT`

Focused command:

```powershell
.\scripts\test-agxiome-tests-isolated.ps1 -Filter "FullyQualifiedName~MongoDbAtlasVectorStoreLiveTests|FullyQualifiedName~CosmosDbVCoreStoreTests|FullyQualifiedName~ScaNNStoreLiveTests|FullyQualifiedName~RocksDbHnswStoreTests|FullyQualifiedName~SqlServerVectorStoreTests"
```

Helper script:

```powershell
.\scripts\test-vector-live-hooks.ps1
.\scripts\test-vector-live-hooks.ps1 -OnlyRocksDb
.\scripts\test-vector-live-hooks.ps1 -OnlyManaged
.\scripts\test-vector-live-hooks.ps1 -OnlyScaNN
```

Current local proof on this workstation:

- `MongoDbAtlasVectorStoreLiveTests` skips until Atlas env vars are provided.
- `CosmosDbVCoreStoreTests` skip their live CRUD/search methods until Cosmos vCore env vars are provided.
- `ScaNNStoreLiveTests` skips until a real wrapper endpoint is provided.
- `RocksDbHnswStoreTests` now run live locally after adding `RocksDbNative 6.2.2`; the focused isolated slice passed `7/7`.
- `SqlServerVectorStoreTests` skip their live methods until a VECTOR-enabled SQL Server connection string is provided.

## Future Backlog

Deferred to future truthful validation backlog:

- `VD-23 MongoDB Atlas`: requires a real managed Atlas cluster and vector index.
- `VD-24 Cosmos DB vCore`: requires a real managed vCore account with vector search enabled.
- `VD-28 ScaNN`: requires the actual production-target wrapper/runtime endpoint.
- `VD-30 SQL Server + vector`: requires a VECTOR-enabled SQL Server runtime.