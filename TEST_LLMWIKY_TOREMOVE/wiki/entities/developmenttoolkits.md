---
title: Development Toolkits
category: entity
tags: [development, toolkit, github, gitlab, bitbucket, api, version-control, ci-cd]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# Development Toolkits

## Summary
The Development Toolkits are a set of agent-integratable tools that provide programmatic access to popular Git hosting platforms (GitHub, GitLab, Bitbucket) and other development‑related services. They follow the `ToolkitBase` pattern, offering a standardised `ExecuteCoreAsync` interface for operations such as repository queries, issue management, pull/merge requests, commit listing, file retrieval, search, workflow triggers, and release browsing. Each toolkit requires minimal configuration (public API calls work without tokens for read‑only GitHub; write operations need authentication). These toolkits empower AI agents to interact with version‑control systems and CI/CD pipelines autonomously.

## Key Details
- **GitHubToolkit** (`T‑48`) – 14 actions: `get_repo`, `list_issues`, `get_issue`, `create_issue`, `list_prs`, `get_pr`, `list_commits`, `get_file`, `search_code`, `list_releases`, `list_workflows`, `trigger_workflow`, `list_branches`. Public read operations work without a token; write operations require a Bearer token (from `"token"` or `"api_key"` setting).
- **GitLabToolkit** (`T‑49`) – 5 actions: `list_projects`, `get_project`, `list_issues`, `list_mrs`, `list_commits`. Requires a `"token"` (private token or OAuth token) and optional `"base_url"` for self‑hosted instances.
- **BitbucketToolkit** (`T‑50`) – actions include repository and issue operations; requires `"username"` and `"app_password"` for Basic authentication.
- Additional toolkits (T‑51 through T‑67) are present in the source but not fully detailed in the excerpt; they likely cover other development services (e.g., Azure DevOps, Jira, etc.).
- All toolkits inherit from `ToolkitBase` and expose `Id`, `Name`, `Category`, and `IsConfigured` properties.
- Configuration is passed via `ToolkitConfig` (key‑value settings). Public GitHub API calls work without configuration.
- HTTP client is shared (`file static class Http`) with a 30‑second timeout.
- Errors return `ToolkitResult.Fail` with a descriptive message; success returns `ToolkitResult.Success` containing a JSON element or object.

## Cross-References
- [[ToolkitBase]] — base class providing the execution contract and parameter handling.
- [[ToolkitConfig]] — configuration object for settings like tokens and base URLs.
- [[ToolkitResult]] — result type returned by all toolkit actions.

## Source Files
- `AGXIOME.Toolkits/Development/DevelopmentToolkits.cs` (ingested 2026-05-14)