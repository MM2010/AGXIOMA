---
goal: AGXIOME F4 DB Providers PLAN_DETAILS
version: 1.0
date_created: 2026-05-16
last_updated: 2026-05-16
owner: AGXIOME implementation track
status: Planned
tags: [feature, planning, database, testing, f4]
---

# Introduction

Questo documento definisce il dettaglio operativo della prossima sezione da implementare secondo il piano autorevole, F4 DB Providers. La sezione viene scelta come prossimo passo per tre motivi: e' il primo blocco realmente aperto nel piano autorevole, abilita storage reale e test realistici richiesti dal processo, ed ha gia' una superficie tecnica concreta nel repo su cui lavorare senza aprire dipendenze speculative.

## 1. Requirements & Constraints

- **REQ-001**: La sezione F4 deve essere affrontata prima di F5/F6/F12 se il criterio e` seguire il piano autorevole in ordine e consolidare i provider reali di persistenza.
- **REQ-002**: Ogni task DB-01..DB-25 deve avere un `PLAN_DETAILS` eseguibile con subtask, file target, matrice test e criteri di uscita.
- **REQ-003**: Tutti i provider devono convergere sull'interfaccia esistente `ICommandStore` in `AGXIOME.Shared/Interfaces/IStorageInterfaces.cs`.
- **REQ-004**: La surface esistente in `AGXIOME.Storage/Stores/*.cs` deve essere riusata e completata, non duplicata.
- **TST-001**: Ogni task deve definire test funzionali, offensivi, aggressivi, osservabilita` e provider reale/emulatore.
- **TST-002**: I test devono preferire container/emulatori/provider reali. I mock sono vietati salvo impossibilita` tecnica del provider esterno.
- **OPS-001**: La validazione AGXIOME.Tests deve usare lo script isolato `AGXIOMA/scripts/test-agxiome-tests-isolated.ps1` se ricompare il lock su `MvcTestingAppManifest.json`.
- **SEC-001**: Tutti gli errori devono rientrare nel boundary exception model AGXIOME: niente stack trace esterni, codici errore strutturati, logging sicuro.
- **CON-001**: Non introdurre una nuova astrazione storage parallela; completare quella esistente.
- **CON-002**: Non dichiarare chiuso un task solo con unit test sintetici; serve almeno una prova con provider reale o emulatore realistico.
- **PAT-001**: Riutilizzare le basi esistenti quando presenti: `NpgsqlCommandStoreBase`, `SqlServerCommandStoreBase`, file aggregati per famiglie provider, fixture F4/F14/F16 gia` presenti.

## 2. Decisione Di Sequenza

- **DEC-001**: Proseguire con **F4 DB Providers** come prossima sezione implementativa.
- **DEC-002**: All'interno di F4, eseguire prima i provider SQL ed embedded che consolidano il contratto (`DB-01`..`DB-08`), poi NoSQL (`DB-09`..`DB-14`), poi KV/cache (`DB-15`..`DB-18`), poi specializzati (`DB-19`..`DB-25`).
- **DEC-003**: Considerare i task gia` parzialmente presenti nel codice come "hardening + verifica completa", non come "done" automatico.

## 3. Shared Section Baseline

### GOAL-001

Stabilire il baseline comune di F4 prima di chiudere singoli provider.

- `TASK-001`: verificare il contratto `ICommandStore` e l'uso coerente in tutti gli store di `AGXIOME.Storage/Stores`.
- `TASK-002`: verificare le registrazioni DI in `AGXIOME.Storage/DependencyInjection/AgxiomeInfraExtensions.cs`.
- `TASK-003`: allineare naming, error model e options validation in tutti gli store F4.
- `TASK-004`: consolidare la suite `AGXIOME.Tests/F4DbStoreTests.cs` come entrypoint principale F4.
- `TASK-005`: definire fixture reali/container/emulatori riusabili per F4 e supplementi F14/F16.

### File Baseline Sezione

- `AGXIOME.Shared/Interfaces/IStorageInterfaces.cs`
- `AGXIOME.Storage/DependencyInjection/AgxiomeInfraExtensions.cs`
- `AGXIOME.Storage/Stores/*.cs`
- `AGXIOME.Tests/F4DbStoreTests.cs`
- `AGXIOME.Tests/F14/F14_TS03_DbProviderTests.cs`
- `AGXIOME.Tests/F16/F16_TS01_RealWorldScenarioTests.cs`

## 4. Task Details

### DB-01 — SQLite

- **Goal**: chiudere `SqliteCommandStore` come baseline embedded reale.
- **Production files**: `AGXIOME.Storage/Stores/SqliteStore.cs`, `AGXIOME.Storage/DependencyInjection/AgxiomeInfraExtensions.cs`.
- **Test files**: `AGXIOME.Tests/F4DbStoreTests.cs`, `AGXIOME.Tests/F14/F14_TS03_DbProviderTests.cs`.
- **SubTASK-01**: verificare bootstrap schema, creazione file db, parametri connessione e disposal sicuro.
- **SubTASK-02**: verificare roundtrip `AppendAsync`, `ReadAllAsync`, `PruneAsync`, `HealthCheckAsync` contro file SQLite reale.
- **SubTASK-03**: aggiungere test di lock reale su file e recovery dopo retry.
- **SubTASK-04**: verificare safe failure su path invalido, db corrotto, directory non accessibile.
- **Functional tests**: init schema, append/read roundtrip, order preservation, prune semantics.
- **Offensive tests**: connection string vuota, file read-only, payload nullo/corrotto.
- **Aggressive tests**: concorrenza multi-writer, file lock contention, dispose/reopen loop.
- **Observability tests**: errore SQLite pseudonimizzato, nessun path sensibile in output.
- **Real provider**: file SQLite temporaneo reale, lock OS reale.
- **Exit criteria**: store stabile con test reali embedded verdi e nessuna dipendenza mock.

### DB-02 — SQL Server

- **Goal**: chiudere `SqlServerCommandStore` come provider enterprise principale Microsoft.
- **Production files**: `AGXIOME.Storage/Stores/SqlServerStores.cs`, `AGXIOME.Storage/DependencyInjection/AgxiomeInfraExtensions.cs`.
- **Test files**: `AGXIOME.Tests/F4DbStoreTests.cs`, fixture container dedicate o da introdurre in `AGXIOME.Tests`.
- **SubTASK-01**: verificare schema bootstrap, transient retry, timeout e validation connection string.
- **SubTASK-02**: confermare compatibilita` con SQL Server Testcontainer reale.
- **SubTASK-03**: coprire concorrenza e idempotenza su append multipli.
- **SubTASK-04**: verificare error mapping per login failure, database offline, command timeout.
- **Functional tests**: append/read, healthcheck, prune, ordering.
- **Offensive tests**: credenziali errate, database inesistente, table missing, schema drift minimo.
- **Aggressive tests**: burst write, connection recycle, transient network interruption simulata via container restart.
- **Observability tests**: eccezioni SqlClient convertite in errori AGXIOME senza leak di secret.
- **Real provider**: SQL Server container reale.
- **Exit criteria**: test container verdi e error contract uniforme col baseline F4.

### DB-03 — SQL Server LocalDB

- **Goal**: chiudere `SqlServerLocalDbCommandStore` come variante dev-only Windows.
- **Production files**: `AGXIOME.Storage/Stores/SqlServerStores.cs`.
- **Test files**: `AGXIOME.Tests/F4DbStoreTests.cs`.
- **SubTASK-01**: verificare attach/detach db locale e path file `.mdf` temporanei.
- **SubTASK-02**: separare chiaramente i test LocalDB da quelli SQL Server container.
- **SubTASK-03**: introdurre skip esplicito fuori da Windows/host senza LocalDB.
- **Functional tests**: create db file, append/read, reopen existing db.
- **Offensive tests**: LocalDB non installato, attach path invalido, file occupato.
- **Aggressive tests**: open/close ripetuto, parallel attach su file diversi.
- **Observability tests**: skip reason chiaro, nessuna failure rumorosa in ambienti incompatibili.
- **Real provider**: LocalDB reale host Windows.
- **Exit criteria**: comportamento dev riproducibile e skip deterministico quando il provider manca.

### DB-04 — Neon

- **Goal**: chiudere `NeonCommandStore` come variante Postgres serverless con connessione custom.
- **Production files**: `AGXIOME.Storage/Stores/NpgsqlVariantStores.cs`.
- **Test files**: `AGXIOME.Tests/F4DbStoreTests.cs`, `AGXIOME.Tests/F16/F16_TS01_RealWorldScenarioTests.cs` se esteso.
- **SubTASK-01**: verificare che la variante riusi `NpgsqlCommandStoreBase` senza logica duplicata.
- **SubTASK-02**: introdurre contract test per endpoint custom, pooling e SSL requirement.
- **SubTASK-03**: separare test locali compatibili Postgres da live contract test Neon via env vars.
- **Functional tests**: roundtrip store su connessione Npgsql custom.
- **Offensive tests**: endpoint malformato, SSL mode incompatibile, credential mismatch.
- **Aggressive tests**: reconnect dopo idle timeout, connection pool exhaustion moderata.
- **Observability tests**: log safe su TLS/handshake failure.
- **Real provider**: Postgres compat locale per baseline + live Neon solo se credenziali presenti.
- **Mock justification**: nessun mock; fallback a compat mode locale se Neon live non disponibile.
- **Exit criteria**: variante stabile e differenziata solo dove il provider lo richiede davvero.

### DB-05 — Supabase

- **Goal**: chiudere `SupabaseCommandStore` come variante Postgres/BaaS.
- **Production files**: `AGXIOME.Storage/Stores/NpgsqlVariantStores.cs`.
- **Test files**: `AGXIOME.Tests/F4DbStoreTests.cs`.
- **SubTASK-01**: verificare riuso base Npgsql e gestione connessione Supabase.
- **SubTASK-02**: testare schema bootstrap e compatibilita` RLS-safe a livello store.
- **SubTASK-03**: introdurre live tests gated via env vars per endpoint Supabase reale.
- **Functional tests**: append/read/prune su compat pg.
- **Offensive tests**: chiave anon/service role errata, db non raggiungibile, table permission denied.
- **Aggressive tests**: reconnect dopo rotating token/connection recycle.
- **Observability tests**: masking di host, key, user id.
- **Real provider**: Postgres compat locale + live Supabase opzionale, senza mock.
- **Exit criteria**: variant store con test di compatibilita` e policy error mapping chiari.

### DB-06 — SingleStore

- **Goal**: chiudere `SingleStoreCommandStore` come SQL distribuito high-throughput.
- **Production files**: `AGXIOME.Storage/Stores/SingleStoreStore.cs`.
- **Test files**: `AGXIOME.Tests/F4DbStoreTests.cs`.
- **SubTASK-01**: verificare compatibilita` connector, ddl bootstrap e upsert/idempotenza.
- **SubTASK-02**: usare container SingleStore o ambiente dev reale dedicato.
- **SubTASK-03**: testare batch append e ordering consistente.
- **Functional tests**: schema create, append/read, prune.
- **Offensive tests**: unsupported syntax, missing privileges, bad connection config.
- **Aggressive tests**: batch insert sotto concorrenza, reconnect dopo restart nodo.
- **Observability tests**: errori SQL distribuiti resi come error code AGXIOME.
- **Real provider**: container/provider reale.
- **Exit criteria**: zero divergenze contrattuali rispetto agli altri SQL provider.

### DB-07 — CockroachDB

- **Goal**: chiudere `CockroachDbCommandStore` come Postgres-distributed variant.
- **Production files**: `AGXIOME.Storage/Stores/NpgsqlVariantStores.cs`.
- **Test files**: `AGXIOME.Tests/F4DbStoreTests.cs`.
- **SubTASK-01**: verificare peculiarita` serializable/retry di Cockroach.
- **SubTASK-02**: introdurre test su retryable transaction errors.
- **SubTASK-03**: testare ordering e read consistency.
- **Functional tests**: append/read/prune su cluster/container Cockroach.
- **Offensive tests**: bad gateway cert, serializable conflict non gestito, invalid db.
- **Aggressive tests**: parallel append con conflict retry.
- **Observability tests**: dettagli retry loggati senza noise eccessivo.
- **Real provider**: container CockroachDB reale.
- **Exit criteria**: variant store compatibile col contratto e robusto ai retry serializable.

### DB-08 — TimescaleDB

- **Goal**: chiudere `TimescaleDbCommandStore` come Postgres time-series variant.
- **Production files**: `AGXIOME.Storage/Stores/NpgsqlVariantStores.cs`, eventuale `TimeSeriesStores.cs` solo se necessario.
- **Test files**: `AGXIOME.Tests/F4DbStoreTests.cs`.
- **SubTASK-01**: verificare bootstrap su schema compatibile Timescale.
- **SubTASK-02**: aggiungere test opzionali su hypertable se la store surface lo usa.
- **SubTASK-03**: assicurare che il provider non diverga dal contratto comando.
- **Functional tests**: roundtrip store su container Timescale.
- **Offensive tests**: extension assente, schema non autorizzato, incompatibilita` SQL.
- **Aggressive tests**: write burst e prune su dataset voluminoso moderato.
- **Observability tests**: errori extension/setup gestiti in modo safe.
- **Real provider**: container TimescaleDB reale.
- **Exit criteria**: variant stabile e riuso massimo del core Npgsql.

### DB-09 — DynamoDB

- **Goal**: chiudere `DynamoDbCommandStore` come KV/doc cloud-native.
- **Production files**: `AGXIOME.Storage/Stores/DynamoDbStore.cs`.
- **Test files**: `AGXIOME.Tests/F4DbStoreTests.cs`.
- **SubTASK-01**: verificare table bootstrap, partition key design e TTL se usato.
- **SubTASK-02**: usare DynamoDB Local o LocalStack, non mock SDK.
- **SubTASK-03**: coprire consistency e overwrite semantics.
- **Functional tests**: create table, put/get/query equivalent, delete/prune.
- **Offensive tests**: invalid region, missing credentials, missing table.
- **Aggressive tests**: hot partition burst, retry throttling, transient unavailable.
- **Observability tests**: error mapping AWS pseudonimizzato.
- **Real provider**: DynamoDB Local o LocalStack.
- **Exit criteria**: provider NoSQL verde su emulator reale e nessun mock di basso livello.

### DB-10 — Cosmos DB

- **Goal**: chiudere `CosmosDbCommandStore` con partizionamento corretto.
- **Production files**: `AGXIOME.Storage/Stores/CosmosDbStore.cs`.
- **Test files**: `AGXIOME.Tests/F4DbStoreTests.cs`.
- **SubTASK-01**: verificare db/container bootstrap e partition key.
- **SubTASK-02**: introdurre test via emulator Cosmos DB, preferibilmente locale/container.
- **SubTASK-03**: coprire optimistic concurrency e RU-related failures.
- **Functional tests**: create container, upsert/get/query/delete.
- **Offensive tests**: bad key, wrong endpoint, partition key mismatch.
- **Aggressive tests**: concurrent upsert, throttling 429 retry, query storm moderato.
- **Observability tests**: request charge e diagnostics non esposti fuori boundary.
- **Real provider**: emulator Cosmos DB.
- **Exit criteria**: comportamento deterministico su emulator e retry policy verificata.

### DB-11 — Firestore

- **Goal**: chiudere `FirestoreCommandStore` come document store Google.
- **Production files**: `AGXIOME.Storage/Stores/FirestoreStore.cs`.
- **Test files**: `AGXIOME.Tests/F4DbStoreTests.cs`.
- **SubTASK-01**: usare Firestore emulator reale.
- **SubTASK-02**: verificare collection/document mapping e ordering query.
- **SubTASK-03**: coprire failure su project/config non validi.
- **Functional tests**: create/read/update/delete/query via emulator.
- **Offensive tests**: credentials/project invalidi, missing emulator endpoint.
- **Aggressive tests**: parallel writes e eventual retry su transient rpc errors.
- **Observability tests**: log safe su project/document identifiers.
- **Real provider**: Firestore emulator.
- **Exit criteria**: suite emulatore verde e mapping documentale coerente.

### DB-12 — SurrealDB

- **Goal**: chiudere `SurrealDbCommandStore` come multi-model provider.
- **Production files**: `AGXIOME.Storage/Stores/SurrealDbStore.cs`.
- **Test files**: `AGXIOME.Tests/F4DbStoreTests.cs`.
- **SubTASK-01**: verificare connessione, namespace/database bootstrap e query base.
- **SubTASK-02**: usare container SurrealDB reale.
- **SubTASK-03**: coprire compatibilita` con append/read e delete semantics.
- **Functional tests**: ns/db init, create/read/query/delete.
- **Offensive tests**: invalid namespace, auth failure, unsupported query path.
- **Aggressive tests**: reconnect dopo restart, concurrent writes.
- **Observability tests**: errori query resi in formato AGXIOME safe.
- **Real provider**: SurrealDB container.
- **Exit criteria**: provider multi-model stabile e coperto.

### DB-13 — RavenDB

- **Goal**: chiudere `RavenDbCommandStore` con session lifecycle corretto.
- **Production files**: `AGXIOME.Storage/Stores/RavenDbAndCouchbaseStores.cs`.
- **Test files**: `AGXIOME.Tests/F4DbStoreTests.cs`.
- **SubTASK-01**: verificare document store bootstrap e indexes minimi.
- **SubTASK-02**: usare RavenDB container reale.
- **SubTASK-03**: testare session disposal, optimistic concurrency e query.
- **Functional tests**: store/load/query/delete.
- **Offensive tests**: bad url, auth failure, missing database.
- **Aggressive tests**: concurrent writes, repeated session open/close.
- **Observability tests**: dettagli cluster non esposti a caller esterno.
- **Real provider**: RavenDB container.
- **Exit criteria**: provider documentale verde con lifecycle corretto.

### DB-14 — Couchbase

- **Goal**: chiudere `CouchbaseCommandStore` come KV/doc cluster provider.
- **Production files**: `AGXIOME.Storage/Stores/RavenDbAndCouchbaseStores.cs`.
- **Test files**: `AGXIOME.Tests/F4DbStoreTests.cs`.
- **SubTASK-01**: verificare bootstrap bucket/scope/collection.
- **SubTASK-02**: usare container Couchbase reale.
- **SubTASK-03**: coprire disposal async e transient node unavailability.
- **Functional tests**: insert/get/query/delete.
- **Offensive tests**: bucket assente, credenziali errate, query service non pronto.
- **Aggressive tests**: parallel read/write e reconnect dopo restart.
- **Observability tests**: masking host/username.
- **Real provider**: Couchbase container.
- **Exit criteria**: provider cluster-safe con `IAsyncDisposable` verificato.

### DB-15 — Redis Session

- **Goal**: chiudere `RedisSessionStore` come store sessionale autorevole non browser-side.
- **Production files**: `AGXIOME.Storage/Stores/RedisSessionStore.cs`, `AGXIOME.Storage/Session/RedisSessionManager.cs`.
- **Test files**: `AGXIOME.Tests/F4DbStoreTests.cs`, `AGXIOME.Tests/F7StorageSessionTests.cs`, `AGXIOME.Tests/F16/F16_TS01_RealWorldScenarioTests.cs`.
- **SubTASK-01**: verificare TTL, key prefixing, ordering event/session append e cleanup.
- **SubTASK-02**: usare Redis reale/container, non in-memory fake.
- **SubTASK-03**: coprire reconnect, key expiry, prefix isolation.
- **Functional tests**: set/get/append/read/prune/ttl.
- **Offensive tests**: bad redis endpoint, corrupted payload, missing db index.
- **Aggressive tests**: reconnect after restart, key storm, concurrent sessions.
- **Observability tests**: log pseudonimizzato su key/session identifiers.
- **Real provider**: Redis container reale.
- **Exit criteria**: compatibile con i requisiti realtime/session del sistema.

### DB-16 — Memcached

- **Goal**: chiudere `MemcachedCommandStore` come cache/KV volatile.
- **Production files**: `AGXIOME.Storage/Stores/MemcachedAndHazelcastStores.cs`.
- **Test files**: `AGXIOME.Tests/F4DbStoreTests.cs`.
- **SubTASK-01**: verificare store semantics supportate e limiti del provider.
- **SubTASK-02**: usare memcached reale in container.
- **SubTASK-03**: definire chiaramente le operazioni non supportabili in modo nativo e il fallback coerente.
- **Functional tests**: put/get/delete/count minimo coerente col provider.
- **Offensive tests**: oversize payload, bad endpoint, deserialize failure.
- **Aggressive tests**: eviction pressure, concurrent set/get storm.
- **Observability tests**: warning chiari su limiti provider senza noise.
- **Real provider**: Memcached container.
- **Exit criteria**: limite funzionale documentato e testato, niente comportamento ambiguo.

### DB-17 — Hazelcast

- **Goal**: chiudere `HazelcastCommandStore` come distributed KV con near-cache.
- **Production files**: `AGXIOME.Storage/Stores/MemcachedAndHazelcastStores.cs`.
- **Test files**: `AGXIOME.Tests/F4DbStoreTests.cs`.
- **SubTASK-01**: verificare client bootstrap, map naming e async disposal.
- **SubTASK-02**: usare Hazelcast reale/container.
- **SubTASK-03**: testare reconnect cluster e map isolation.
- **Functional tests**: put/get/delete/readall equivalente.
- **Offensive tests**: cluster name errato, serialization error, auth failure.
- **Aggressive tests**: reconnect after restart, concurrent writes, map churn.
- **Observability tests**: log safe su cluster metadata.
- **Real provider**: Hazelcast container.
- **Exit criteria**: store distribuito stabile e cluster-safe.

### DB-18 — Etcd

- **Goal**: chiudere `EtcdCommandStore` come distributed KV a semantica forte.
- **Production files**: `AGXIOME.Storage/Stores/EtcdStore.cs`.
- **Test files**: `AGXIOME.Tests/F4DbStoreTests.cs`.
- **SubTASK-01**: verificare key space prefix, compare-and-swap se usato, lease/ttl.
- **SubTASK-02**: usare Etcd container reale.
- **SubTASK-03**: coprire endpoint failover e watch-related edge cases solo se la store surface li usa.
- **Functional tests**: put/get/delete/health.
- **Offensive tests**: bad endpoint, lease failure, invalid key prefix.
- **Aggressive tests**: reconnect, repeated writes, compaction pressure moderata.
- **Observability tests**: gRPC errors mappati correttamente.
- **Real provider**: Etcd container.
- **Exit criteria**: provider KV forte con error handling affidabile.

### DB-19 — ClickHouse

- **Goal**: chiudere `ClickHouseCommandStore` come OLAP/append-heavy provider.
- **Production files**: `AGXIOME.Storage/Stores/ClickHouseStore.cs`.
- **Test files**: `AGXIOME.Tests/F4DbStoreTests.cs`.
- **SubTASK-01**: verificare table bootstrap, append model e query compatibili.
- **SubTASK-02**: usare ClickHouse container reale.
- **SubTASK-03**: definire esplicitamente i limiti di delete/prune se implementati come soft-delete.
- **Functional tests**: append/read/query count.
- **Offensive tests**: invalid engine, bad endpoint, unsupported mutation path.
- **Aggressive tests**: burst append, query after insert, restart recovery.
- **Observability tests**: errori HTTP/native mappati in modo uniforme.
- **Real provider**: ClickHouse container.
- **Exit criteria**: provider specializzato usabile senza comportamento nascosto.

### DB-20 — InfluxDB

- **Goal**: chiudere `InfluxDbCommandStore` come time-series specialistico.
- **Production files**: `AGXIOME.Storage/Stores/TimeSeriesStores.cs`.
- **Test files**: `AGXIOME.Tests/F4DbStoreTests.cs`.
- **SubTASK-01**: verificare bucket/org bootstrap e point serialization.
- **SubTASK-02**: usare InfluxDB reale/container.
- **SubTASK-03**: testare query/time window coerenti.
- **Functional tests**: write/read/query/prune baseline.
- **Offensive tests**: bad token, bucket assente, malformed point.
- **Aggressive tests**: point burst, reconnect, query after restart.
- **Observability tests**: token masking e error mapping.
- **Real provider**: InfluxDB container.
- **Exit criteria**: provider time-series stabile e testato con bucket reale.

### DB-21 — QuestDB

- **Goal**: chiudere `QuestDbCommandStore` come variante time-series Postgres-compatible.
- **Production files**: `AGXIOME.Storage/Stores/TimeSeriesStores.cs`.
- **Test files**: `AGXIOME.Tests/F4DbStoreTests.cs`.
- **SubTASK-01**: verificare bootstrap compat PG/ILP dove rilevante.
- **SubTASK-02**: usare QuestDB reale/container.
- **SubTASK-03**: coprire ordering e timestamp precision.
- **Functional tests**: append/read/query.
- **Offensive tests**: schema mismatch, invalid timestamp, bad endpoint.
- **Aggressive tests**: write burst e query rolling window.
- **Observability tests**: errori Quest/PG compat resi in formato AGXIOME.
- **Real provider**: QuestDB container.
- **Exit criteria**: provider time-series compatibile e preciso sui timestamp.

### DB-22 — DuckDB

- **Goal**: chiudere `DuckDbCommandStore` come analytical embedded provider.
- **Production files**: `AGXIOME.Storage/Stores/DuckDbStore.cs`.
- **Test files**: `AGXIOME.Tests/F4DbStoreTests.cs`.
- **SubTASK-01**: verificare file db, schema bootstrap e concurrency limitations note.
- **SubTASK-02**: testare import/export se parte della surface.
- **SubTASK-03**: coprire file lock e reopen.
- **Functional tests**: create/open/append/read/prune.
- **Offensive tests**: path invalido, readonly db, corrupt file.
- **Aggressive tests**: repeated open/close, query after reopen, file contention moderata.
- **Observability tests**: errori file/db safe e riproducibili.
- **Real provider**: DuckDB embedded reale.
- **Exit criteria**: provider embedded verde e distinto da SQLite per use case analitico.

### DB-23 — Elasticsearch

- **Goal**: chiudere `ElasticsearchCommandStore` come search/doc store.
- **Production files**: `AGXIOME.Storage/Stores/SearchStores.cs`.
- **Test files**: `AGXIOME.Tests/F4DbStoreTests.cs`.
- **SubTASK-01**: verificare index bootstrap, mapping e bulk semantics.
- **SubTASK-02**: usare Elasticsearch container reale.
- **SubTASK-03**: coprire delete/index refresh e search filter contract.
- **Functional tests**: index/get/search/delete/count.
- **Offensive tests**: mapping conflict, invalid credentials, cluster unavailable.
- **Aggressive tests**: bulk ingest, refresh pressure, restart recovery.
- **Observability tests**: URI/index name masked nei log.
- **Real provider**: Elasticsearch container.
- **Exit criteria**: provider search documentato e coerente col contratto minimo.

### DB-24 — Meilisearch

- **Goal**: chiudere `MeilisearchCommandStore` come full-text store leggero.
- **Production files**: `AGXIOME.Storage/Stores/SearchStores.cs`.
- **Test files**: `AGXIOME.Tests/F4DbStoreTests.cs`.
- **SubTASK-01**: verificare index creation, async task polling e search semantics.
- **SubTASK-02**: usare Meilisearch container reale.
- **SubTASK-03**: coprire api key assente/errata e task failure.
- **Functional tests**: create index, add docs, search, delete.
- **Offensive tests**: malformed host, unauthorized key, invalid document payload.
- **Aggressive tests**: rapid reindex, task queue pressure, restart recovery.
- **Observability tests**: errori async task resi deterministicamente.
- **Real provider**: Meilisearch container.
- **Exit criteria**: provider full-text leggero stabile e senza mock.

### DB-25 — JSON File

- **Goal**: chiudere `JsonFileCommandStore` come file-based fallback trasparente.
- **Production files**: `AGXIOME.Storage/Stores/JsonFileStore.cs`.
- **Test files**: `AGXIOME.Tests/F4DbStoreTests.cs`, `AGXIOME.Tests/F14/F14_TS03_DbProviderTests.cs`.
- **SubTASK-01**: verificare atomic write, directory bootstrap, file rotation se presente.
- **SubTASK-02**: testare resilience su file corrotto e recovery path.
- **SubTASK-03**: coprire file lock reale e concurrent access.
- **Functional tests**: create/read/update/delete/prune roundtrip.
- **Offensive tests**: invalid JSON, permission denied, path traversal attempt.
- **Aggressive tests**: write storm, lock contention, reopen after partial write.
- **Observability tests**: path masking e safe error surface.
- **Real provider**: file system reale.
- **Exit criteria**: fallback locale robusto e sicuro su disco.

## 5. Alternatives

- **ALT-001**: Partire da F12 Toolkit. Scartato perche' moltiplica integrazioni esterne prima di consolidare persistenza e test provider reali.
- **ALT-002**: Partire da F8 Tools Infra. Scartato come prossimo blocco perche' il piano autorevole colloca prima F4 e l'utente ha dato priorita` a provider reali DB/cache.
- **ALT-003**: Chiudere solo singoli bug spot senza sezione. Scartato perche' produce avanzamento opaco e non lascia traccia pianificata task-by-task.

## 6. Dependencies

- **DEP-001**: `AGXIOME.Shared/Interfaces/IStorageInterfaces.cs`
- **DEP-002**: `AGXIOME.Storage/DependencyInjection/AgxiomeInfraExtensions.cs`
- **DEP-003**: provider packages gia` referenziati o da verificare nei `.csproj` storage/tests
- **DEP-004**: Docker/Testcontainers/emulator locali per Redis, SQL Server, Cockroach, Timescale, DynamoDB Local, Cosmos emulator, Firestore emulator, SurrealDB, RavenDB, Couchbase, ClickHouse, InfluxDB, QuestDB, Elasticsearch, Meilisearch
- **DEP-005**: script di test isolato `AGXIOMA/scripts/test-agxiome-tests-isolated.ps1`

## 7. Files

- **FILE-001**: `AGXIOME.Storage/Stores/SqliteStore.cs`
- **FILE-002**: `AGXIOME.Storage/Stores/SqlServerStores.cs`
- **FILE-003**: `AGXIOME.Storage/Stores/NpgsqlVariantStores.cs`
- **FILE-004**: `AGXIOME.Storage/Stores/SingleStoreStore.cs`
- **FILE-005**: `AGXIOME.Storage/Stores/DynamoDbStore.cs`
- **FILE-006**: `AGXIOME.Storage/Stores/CosmosDbStore.cs`
- **FILE-007**: `AGXIOME.Storage/Stores/FirestoreStore.cs`
- **FILE-008**: `AGXIOME.Storage/Stores/SurrealDbStore.cs`
- **FILE-009**: `AGXIOME.Storage/Stores/RavenDbAndCouchbaseStores.cs`
- **FILE-010**: `AGXIOME.Storage/Stores/RedisSessionStore.cs`
- **FILE-011**: `AGXIOME.Storage/Stores/MemcachedAndHazelcastStores.cs`
- **FILE-012**: `AGXIOME.Storage/Stores/EtcdStore.cs`
- **FILE-013**: `AGXIOME.Storage/Stores/ClickHouseStore.cs`
- **FILE-014**: `AGXIOME.Storage/Stores/TimeSeriesStores.cs`
- **FILE-015**: `AGXIOME.Storage/Stores/DuckDbStore.cs`
- **FILE-016**: `AGXIOME.Storage/Stores/SearchStores.cs`
- **FILE-017**: `AGXIOME.Storage/Stores/JsonFileStore.cs`
- **FILE-018**: `AGXIOME.Storage/DependencyInjection/AgxiomeInfraExtensions.cs`
- **FILE-019**: `AGXIOME.Tests/F4DbStoreTests.cs`
- **FILE-020**: `AGXIOME.Tests/F14/F14_TS03_DbProviderTests.cs`
- **FILE-021**: `AGXIOME.Tests/F16/F16_TS01_RealWorldScenarioTests.cs`

## 8. Testing

- **TEST-001**: ogni task F4 deve avere almeno 1 test funzionale di roundtrip.
- **TEST-002**: ogni task F4 deve avere almeno 1 test offensivo su config/connessione/payload invalido.
- **TEST-003**: ogni task F4 deve avere almeno 1 test aggressivo su concorrenza/reconnect/restart/lock.
- **TEST-004**: ogni task F4 deve avere almeno 1 test di osservabilita`/safe error behavior.
- **TEST-005**: i provider embedded devono essere testati su file reali; i provider remoti su container/emulatori reali o live env gated.
- **TEST-006**: la suite F4 deve essere eseguibile via filtro mirato prima della suite isolata completa.

## 9. Risks & Assumptions

- **RISK-001**: alcuni provider cloud/BaaS non hanno emulator locale completo; serve una strategia "compat locale + live env gated" senza ricadere nei mock.
- **RISK-002**: i lock del progetto test possono nascondere regressioni se non si usa il runner isolato.
- **RISK-003**: i provider aggregati nello stesso file possono favorire regressioni trasversali se modificati senza test mirati.
- **ASSUMPTION-001**: la surface `ICommandStore` attuale e` sufficiente e non richiede redesign prima della chiusura F4.
- **ASSUMPTION-002**: Docker/Testcontainers sono disponibili in locale per la maggioranza dei provider F4.

## 10. Next Execution Order

1. Baseline shared F4.
2. DB-01 SQLite.
3. DB-02 SQL Server.
4. DB-03 SQL Server LocalDB.
5. DB-04..DB-08 varianti SQL/Npgsql.
6. DB-09..DB-14 NoSQL.
7. DB-15..DB-18 KV/cache.
8. DB-19..DB-25 specialized.
