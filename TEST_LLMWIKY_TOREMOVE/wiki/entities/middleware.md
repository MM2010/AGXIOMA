---
title: Middleware
category: entity
tags: [middleware, security, auth, rbac, rate-limiting, error-handling, aspnetcore]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# Middleware

## Summary
The AGXIOME runtime defines a stateless middleware pipeline that enforces security, authentication, authorization, rate limiting, and error handling for all HTTP requests. Each middleware component is injected via `IServiceProvider` and follows a strict ordering: `SecurityHeadersMiddleware` runs first to ensure hardened headers on every response, followed by `AgxiomeAuthMiddleware`, `RbacMiddleware`, `RateLimiterMiddleware`, and finally `ErrorHandlerMiddleware` as the outermost catch-all. Together they provide a consistent, secure, and rate-controlled API surface.

## Key Details
- **All middleware is stateless** — dependencies (e.g., `IApiKeyStore`, `IRbacPolicy`, `IRateLimiter`) are resolved per-scope via `IServiceProvider`.
- **SecurityHeadersMiddleware** adds headers: `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: strict-origin-when-cross-origin`, `Permissions-Policy`, `X-XSS-Protection`, and `Content-Security-Policy` with restrictive defaults.
- **AgxiomeAuthMiddleware** validates the `X-Api-Key` header against `IApiKeyStore`; on success creates an `AgxiomePrincipal` stored in `HttpContext.Items["AgxiomePrincipal"]`. Public paths (`/health`, `/hubs/*`) skip authentication. Failed auth returns `401` with structured JSON error and logs via `AuditLogger`.
- **RbacMiddleware** maps HTTP method + route prefix to required `AgxiomePermission` (e.g., `POST /api/agents` → `AgentCreate`). Uses `IRbacPolicy.IsAuthorized` and returns `403` if permission is missing. Unauthenticated requests (no principal) pass through.
- **RateLimiterMiddleware** applies a sliding-window rate limit keyed by `ip:{ip}:{route}` using a default policy of 60 requests per minute. On violation returns `429` with `Retry-After` header and JSON body.
- **ErrorHandlerMiddleware** catches all unhandled exceptions. In development environment stack traces are included; in production a generic message is returned. `OperationCanceledException` from client disconnects is silently ignored.
- All error responses are `application/json` with `{ code, message }` structure.

## Cross-References
- [[Security]] — defines the `IApiKeyStore`, `IRbacPolicy`, `IAuditLogger`, and related types used by the middleware.
- [[Principal]] — the `AgxiomePrincipal` object that carries identity, roles, and scopes.
- [[Pipelines]] — describes how middleware is registered and ordered in the application startup.

## Source Files
- `AGXIOME.Runtime/Middleware/Middleware.cs` (ingested 2026-05-14)