---
title: EnterpriseToolkits
category: entity
tags: [enterprise, toolkit, salesforce, hubspot, zendesk, servicenow]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# EnterpriseToolkits

## Summary
The EnterpriseToolkits class (in the `AGXIOME.Toolkits.Enterprise` namespace) implements a collection of pre-built toolkits for integrating with popular enterprise SaaS platforms. Each toolkit is a sealed subclass of `ToolkitBase`, exposing a set of REST API actions via a shared HTTP client. Authentication methods vary per platform (OAuth2 Bearer token, API key, Basic auth, or username/password). These toolkits are registered as agents in the framework under the `ToolkitCategory.Enterprise` category.

## Key Details
- **Namespace**: `AGXIOME.Toolkits.Enterprise`
- **Base class**: All toolkits extend `ToolkitBase` and implement `ExecuteCoreAsync` with a string-based action switch.
- **Shared HTTP client**: A file-scoped static class `Http` provides a single `HttpClient` with a 30-second timeout.
- **Toolkits implemented**:
  - **SalesforceToolkit** (ID: `salesforce`) — Requires `access_token` and `instance_url` (OAuth2). Actions: `query`, `get_object`, `list_objects`, `describe`.
  - **HubSpotToolkit** (ID: `hubspot`) — Requires `api_key` (Bearer token). Actions: `list_contacts`, `get_contact`, `list_companies`, `list_deals`, `list_tickets`, `get_owner`.
  - **ZendeskToolkit** (ID: `zendesk`) — Requires `subdomain`, `email`, and `api_token` (Basic auth). Actions: `list_tickets`, `get_ticket`, `list_users`, `search`.
  - **ServiceNowToolkit** (ID: `servicenow`) — Informative stub; requires `instance_url`, `username`, `password`. Actions not fully defined in snippet.
- **Configuration**: Each toolkit uses `ToolkitConfig` and retrieves settings via `GetSetting()`.
- **Return type**: All actions return `ToolkitResult` containing a JSON element on success.
- **Source annotations**: Each toolkit is decorated with `[Toolkit(...)]` attribute specifying ID, display name, and category.

## Cross-References
- [[ToolkitBase]] — Base class for all agent toolkits.
- [[ToolkitCategory]] — Enum defining enterprise, productivity, code, etc.
- [[ToolkitResult]] — Result wrapper returned by toolkit executions.
- [[ToolkitConfig]] — Configuration object injected into toolkit constructors.

## Source Files
- `AGXIOME.Toolkits/Enterprise/EnterpriseToolkits.cs` (ingested 2026-05-14)