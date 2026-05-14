---
title: Program
category: entity
tags: [runtime, middleware, security, signalr, aspnetcore, di, configuration]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# Program

## Summary
The `Program.cs` file is the entry point and host builder for the AGXIOMA Runtime (RT-01). It configures a full ASP.NET Core 10 application that acts as a real-time + REST gateway, wiring together the middleware pipeline, dependency injection, CORS, SignalR hubs, background workers, and security services. The middleware order is fixed to enforce security headers, authentication, RBAC, rate limiting, and error handling before controllers and SignalR hubs execute.

## Key Details
- **Framework**: ASP.NET Core 10, running as a WebApplication host.
- **Middleware Order** (top to bottom): SecurityHeaders → ErrorHandler → CORS → Auth (AgxiomeAuth) → RBAC → RateLimiter → Controllers & Hubs.
- **SignalR Hubs**: Maps three hubs at `/hubs/agent`, `/hubs/log`, `/hubs/admin`.
- **Health Endpoint**: `GET /health` returns JSON with status and timestamp.
- **Security Services** (all registered as singletons): `IKeyRotationService`, `IApiKeyStore`, `IRbacPolicy`, `IAuditTrail`, `AuditLogger`, `IRateLimiter`, `RateLimitPolicyRegistry`, `IInputSanitizer`, `ISecretsScanner`.
- **Background Workers**: `BackgroundRunWorker`, `SessionCleanupJob`, `TokenRotationJob` (hosted services).
- **Bootstrap Key**: If `AGXIOME_BOOTSTRAP_KEY` environment variable or config key is set, an admin API key is seeded at startup (development/smoke-test only; not for production).
- **CORS**: Uses `AgxiomeDefault` policy; development defaults to `localhost:3000` and `localhost:5173`; production requires explicit config from `Cors:AllowedOrigins`.
- The partial class `Program` is declared to make the class accessible for integration testing via `WebApplicationFactory`.

## Cross-References
- [[SecurityHeadersMiddleware]] — first middleware in the pipeline.
- [[AgxiomeAuthMiddleware]] — authentication middleware for API keys.
- [[RbacMiddleware]] — role-based access control enforcement.
- [[RateLimiterMiddleware]] — rate limiting via sliding window algorithm.
- [[AgentHub]], [[LogHub]], [[AdminHub]] — SignalR hubs for real-time communication.
- [[IApiKeyStore]], [[IRbacPolicy]], [[IRateLimiter]] — core security service interfaces.
- [[BackgroundRunWorker]], [[SessionCleanupJob]], [[TokenRotationJob]] — background workers for runtime tasks.

## Source Files
- `AGXIOME.Runtime/Program.cs` (ingested 2026-05-14)