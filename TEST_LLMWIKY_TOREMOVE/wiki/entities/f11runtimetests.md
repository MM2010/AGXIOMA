---  
title: F11 Runtime Tests  
category: entity  
tags: [tests, runtime, integration, unit, middleware, authentication, authorization, rate-limiting]  
source_count: 1  
created_at: 2026-05-14  
updated_at: 2026-05-14  
---  

# F11 Runtime Tests  

## Summary  
The F11 Runtime Tests suite validates the core AgentOS runtime layer, including middleware, controllers, SignalR hubs, and background workers. It contains 142 tests, split into integration tests (using `WebApplicationFactory<Program>`) and unit tests for logic classes. These tests ensure that authentication (API key), role-based access control (RBAC), rate limiting, audit logging, and input sanitization function correctly, and verify that the dependency injection scaffold, health endpoint, and security headers are properly configured.  

## Key Details  
- **Total tests**: 142 across multiple test classes (`RT01` through `RT10`).  
- **Test groups**:  
  - **RT01** – Program/DI scaffold: health endpoint, service registration (e.g., `IKeyRotationService`, `IApiKeyStore`, `IRbacPolicy`, `IRateLimiter`, `AuditLogger`).  
  - **RT02** – Auth middleware: missing/invalid API key returns 401, valid key passes, public health bypass, security headers, error body format.  
  - **RT03** – RBAC middleware: permissions for Viewer, Admin, Operator roles, multi-role union, empty roles denial, permission enumeration.  
  - **RT04** – Rate limiter: sliding window allows up to limit, returns 429 data when exceeded.  
  - **RT05** – Audit logging: successful and failed actions logged, log includes principal, action, timestamp.  
  - **RT06** – Input sanitization: HTML encoding, script removal, SQL injection prevention, allowed tags retained.  
  - **RT07** – API key rotation: new key issued, old key expires, rotation respects grace period.  
  - **RT08** – Controllers (agent CRUD, run, logs): authorization checks, pagination, validation.  
  - **RT09** – SignalR hubs (AgentHub, LogHub): connection/disconnection, message forwarding, group management.  
  - **RT10** – Background workers (key rotation worker, audit log cleanup): lifecycle, async operation, error resilience.  
- **Testing frameworks**: xUnit, `WebApplicationFactory` for integration, NSubstitute for mocking.  
- **Source file**: `AGXIOME.Tests/F11RuntimeTests.cs`.  

## Cross-References  
- [[entities/f10runtime]] – The runtime layer under test.  
- [[entities/f02auth]] – Authentication and API key validation middleware.  
- [[entities/f03rbac]] – Role-based access control implementation.  
- [[entities/f04ratelimit]] – Rate limiting policy and sliding window logic.  
- [[entities/f05audit]] – Audit logging service.  
- [[entities/f06sanitize]] – Input sanitization utilities.  
- [[entities/f09workers]] – Background worker infrastructure.  

## Source Files  
- `AGXIOME.Tests/F11RuntimeTests.cs` (ingested 2026-05-14)