# Aggressive Pre-Production Security Attack Report

## Scope

Assessed live AGXIOMA samples in a sandbox with local Dockerized dependencies and attack tooling.

| Target | Mode | URL / Port | Notes |
| --- | --- | --- | --- |
| RAG Pipeline | in-memory | `http://127.0.0.1:18081` | published sample, mock local Ollama for embeddings |
| AGXIOME.Runtime | Redis-backed | `http://127.0.0.1:18082` | bootstrap API key seeded, Redis on `16379` |
| Support Hub | relational | `http://127.0.0.1:18083` | enterprise mode, PostgreSQL on `15432`, Redis on `16379` |
| Support Hub | Vault-backed deployment | `http://127.0.0.1:18084` | same app, secrets pulled from local Vault on `18200` |

> The repository does not currently contain a first-class Vault-backed sample application. The Vault target here is therefore a deployment variant used to test secrets delivery and adjacent attack surface.

## Tooling used

- Dockerized `nmap` for service discovery
- Dockerized ZAP baseline scans
- Dockerized `redis-cli`
- Dockerized `psql`
- direct HTTP abuse with `curl` and concurrent local request bursts

## Executive result

The strongest issues were on the Support Hub samples, not on the RAG sample.

1. **Critical**: Support Hub exposes sensitive backoffice workflows anonymously and allows unauthenticated state changes.
2. **High**: Redis and PostgreSQL were directly reachable from the network with no meaningful protection in the test deployment.
3. **Medium**: the Redis-backed runtime returns `500` on an authenticated dynamic-agent read path, indicating an incomplete runtime registration / easy authenticated DoS path.
4. **Positive**: the in-memory RAG sample resisted the main malicious input probes I attempted.

## Findings

### 1. Critical — anonymous backoffice creation, listing, confirmation, and closure

**Targets:** relational sample `:18083`, Vault-backed sample `:18084`

The Support Hub exposes these endpoints anonymously:

- `POST /backoffice`
- `GET /backoffice/dashboard`
- `GET /backoffice/runs`
- `GET /backoffice/{runId}`
- `POST /backoffice/confirm`
- `PATCH /backoffice/{runId}`

I was able to:

1. create a run anonymously;
2. receive the `confirmationToken` in the creation response;
3. list runs anonymously;
4. confirm a sensitive account workflow anonymously using the leaked token;
5. close a run anonymously.

**Evidence**

- Anonymous creation returned a live token and workflow state:

```json
{"runId":"019e3bde-06d5-7e9b-8404-90892fca8b8d","status":"paused-awaiting-confirmation","confirmationToken":"019e3bde06da74909ae89942f93ac848"}
```

- Anonymous confirmation with the leaked token advanced the case and opened a ticket:

```json
{"runId":"019e3bde-06d5-7e9b-8404-90892fca8b8d","status":"confirmed","ticketId":"BO-20260518-bce8ddb2"}
```

- Anonymous close succeeded:

```json
{"runId":"019e3bda-40a3-7e2c-af2e-a4510a06b949","status":"closed"}
```

**Impact**

An unauthenticated attacker can create, inspect, advance, and close backoffice cases and obtain operational identifiers and workflow state. This is a direct integrity and confidentiality issue.

## 2. High — operational data exposed anonymously

**Targets:** relational sample `:18083`, Vault-backed sample `:18084`

Anonymous listing endpoints returned live operational data, including run IDs, areas, statuses, checkpoints, and dashboard counts.

**Evidence**

`GET /backoffice/runs` returned:

```json
[{"runId":"019e3bda-40a3-7e2c-af2e-a4510a06b949","area":"billing","status":"paused-awaiting-confirmation"}]
```

Vault-backed variant `GET /backoffice/dashboard` returned:

```json
{"runtimeMode":"enterprise","canonicalStore":"postgres","hotStore":"redis","runsTotal":1,"notificationsOutbox":1}
```

**Impact**

This allows passive enumeration of internal workflows and active targeting of run IDs without authentication.

## 3. High — Redis directly reachable without auth

**Target:** Redis backing store on `16379`

The backing Redis instance was reachable from a Docker container and accepted unauthenticated commands.

**Evidence**

```text
PONG
agxiome:backoffice:runs:BackofficeRunState:019e3bda-40a3-7e2c-af2e-a4510a06b949
```

**Impact**

An attacker on the same network can inspect or tamper with hot-store state directly, bypassing application controls.

## 4. High — PostgreSQL directly reachable with known credentials

**Target:** PostgreSQL backing store on `15432`

The backing PostgreSQL instance was reachable from a Docker container with the configured credentials.

**Evidence**

`psql` listed the schema successfully and returned live data counts:

```text
public | backofficerunstates | table | agxiome
...
count
-------
2
```

**Impact**

If the deployment keeps these credentials or network exposure patterns, an attacker can inspect or modify canonical workflow data directly.

## 5. Medium — authenticated runtime path returns `500`

**Target:** Redis-backed runtime `:18082`

`GET /api/agents/user/redteam` behaved differently based on auth:

- without API key: `401`
- with valid bootstrap key: `500`

The controller requires `DynamicAgentRegistry`, but `Program.cs` does not register the dynamic runtime services. This makes the authenticated path fail with a server error instead of returning a controlled empty result or a clear feature-disabled response.

**Evidence**

Authenticated body:

```json
{"code":"ERR-0001","numericCode":1,"message":"Errore interno del sistema.","category":"Generic"}
```

**Impact**

Low-effort authenticated requesters can trigger avoidable server-side failures on exposed runtime endpoints.

## 6. Deployment-only note — Vault dev-mode secrets were readable

**Target:** local Vault deployment on `18200`

Because the repo lacks a native Vault-backed sample, I stood up a Vault-injected deployment variant. In that test setup, the Vault dev root token was known and the stored secrets were retrievable.

**Evidence**

```json
{"data":{"data":{"bootstrap_key":"agx-test-key","postgres_conn":"Host=127.0.0.1;Port=15432;Database=agxiome_backoffice;Username=agxiome;Password=agx_pw","redis_conn":"127.0.0.1:16379"}}}
```

**Interpretation**

This is a real deployment risk for any dev-mode or weakly protected Vault usage, but it is **not** a library defect in AGXIOMA itself.

## Controls that held under attack

### RAG in-memory sample

The RAG sample behaved well under the probes I ran:

- unauthenticated `/ingest` returned `401`
- path traversal (`../../etc/passwd`) returned `400`
- symlink / reparse-point ingest attempt returned `400`
- malformed JSON returned `400`
- rate limiting tripped after repeated authenticated requests:

```text
[200, 200, 200, 200, 200, 200, 200, 200, 200, 200, 200, 200, 200, 200, 200, 200, 200, 200, 200, 200, 429, 429, 429, 429, 429]
```

### Broader HTTP sweep

Dockerized ZAP baseline scans did not add higher-signal findings beyond the direct abuse results above. They mainly observed shallow reachable surfaces and network failures during spidering.

## Recommended fixes

1. Require authentication and explicit authorization on every `backoffice` read/write endpoint.
2. Never return raw confirmation tokens in create responses; deliver them out-of-band only.
3. Treat run IDs and ticket IDs as sensitive and stop exposing them anonymously.
4. Remove direct host-port exposure for Redis and PostgreSQL in production-like deployments, or enforce strong auth plus network isolation.
5. Ensure runtime controllers are only mapped when their required services are registered, or return controlled feature-disabled responses instead of `500`.
6. For any Vault-backed deployment, avoid dev mode, avoid known bootstrap tokens, and restrict network reachability to Vault.
