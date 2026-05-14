---
title: Rbac
category: entity
tags: [rbac, security, authorization]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# Rbac

## Summary
Role-Based Access Control (RBAC) is the authorization backbone of the AGXIOME framework, enforcing Zero Trust by requiring every request to be authorized before reaching domain logic. It provides four built-in roles (Admin, Operator, Viewer, Service) with a hierarchical permission model, a declarative policy builder for custom rules, and an `IRbacPolicy` interface to evaluate access for authenticated principals (`AgxiomePrincipal`). Service-role principals are further scoped to specific agents or toolkits via `Scopes`.

## Key Details
- **Four built-in roles** (`AgxiomeRole`): Admin (full control), Operator (run agents, manage sessions, view logs), Viewer (read-only), Service (machine-to-machine, scoped to specific resources).
- **Permissions** (`AgxiomePermission`): Granular actions across Agent, Session, Team, Workflow, Knowledge, and Admin domains, each identified by a numeric code (e.g., `AgentCreate = 100`).
- **Default policy hierarchy**: Admin permissions are a superset of Operator, which is a superset of Viewer; Service has minimal run/read permissions.
- **`IRbacPolicy` interface**: Defines `IsAuthorized(principal, permission)`, `HasRole(principal, role)`, and `GetPermissions(role)` for runtime authorization checks.
- **`AgxiomePrincipal`**: A value object holding a `SubjectId` (pseudonymized identity), a list of roles, and optional `Scopes` (e.g., `["agent:demo-agent"]`) to constrain Service role access.
- **`DefaultRbacPolicy`**: Built-in `IRbacPolicy` implementation mapping roles to permissions as described.
- **`RbacPolicyBuilder`**: Fluent builder to create custom policies; supports `Allow()`, `AllowAll()`, and `WithDefaults()` to start from the built-in grants.
- **Zero Trust enforcement**: Every request must be authorized via policy before any domain logic executes.

## Cross-References
- [[entities/security]] — Overall security module, authentication, and authorization principles.
- [[entities/agent]] — Agents are protected by Agent permissions (create, read, run, etc.).
- [[entities/session]] — Sessions are managed via Session permissions.

## Source Files
- `AGXIOME.Security/Rbac.cs` (ingested 2026-05-14)