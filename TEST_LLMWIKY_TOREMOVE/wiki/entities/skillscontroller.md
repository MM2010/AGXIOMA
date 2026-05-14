---
title: SkillsController
category: entity
tags: [controller, rest, skills, api, trust]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# SkillsController

## Summary
The `SkillsController` is an ASP.NET Core REST API controller that manages user skills imported from the Skills.sh marketplace. It provides endpoints for searching the marketplace, importing a skill for a user, listing a user's skills, and updating the trust status of an imported skill. The controller delegates actual operations to `SkillsShClient` (marketplace interaction) and `SkillsShRegistry` (local storage and status tracking).

## Key Details
- **Base path**: `/api/skills`
- **Endpoints**:
  - `GET /api/skills/search?q=&limit=` – Searches the Skills.sh marketplace; returns `BadRequest` if `q` is empty.
  - `POST /api/skills/{skillId}/import/{userId}` – Imports a skill from marketplace, sets initial status to "Pending trust"; returns `NotFound` if skill does not exist.
  - `GET /api/skills/user/{userId}` – Returns all imported skills for a user with metadata (SkillId, Name, Description, Type, Status, ImportedAt).
  - `PATCH /api/skills/{skillId}/trust?userId=` – Accepts a JSON body with `Status` string; updates trust status to `Trusted`, `Pending`, or `Blocked`; returns `NoContent`.
- **Dependencies**: `SkillsShClient` (constructor injected), `SkillsShRegistry` (constructor injected)
- **Data transfer**: Uses `SetSkillTrustRequest` record with a single `Status` string property.
- **Error handling**: Returns `BadRequest` for invalid input (missing query, invalid enum) and `NotFound` when a marketplace skill is not found.
- **Asynchronous**: All endpoints use `async` with `CancellationToken` for scalability.

## Cross-References
- [[SkillsShClient]] – Handles HTTP calls to the Skills.sh marketplace API.
- [[SkillsShRegistry]] – Persists and manages user skill entries and their trust status.
- [[SkillStatus]] – Enum used to define trust states (Trusted, Pending, Blocked).

## Source Files
- `AGXIOME.Runtime/Controllers/SkillsController.cs` (ingested 2026-05-14)