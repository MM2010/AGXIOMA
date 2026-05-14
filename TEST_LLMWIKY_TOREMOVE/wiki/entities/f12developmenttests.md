---
title: F12 Development Tests
category: entity
tags: [tests, F12, development, toolkits]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# F12 Development Tests

## Summary
This test suite verifies the basic structure and execution behavior of all development‑oriented toolkits in the AGXIOME framework. Each toolkit class is tested for correct identity properties (`Id`, `Name`, `Category`) and for returning a `Skipped` status when unconfigured and an `Ok` status when a valid configuration is supplied.

## Key Details
- Located in the namespace `AGXIOME.Tests.F12`.
- Contains dedicated test classes for: `GitHubToolkit`, `GitLabToolkit`, `BitbucketToolkit`, `GitToolkit`, `NuGetToolkit`, `DockerToolkit`, `KubernetesToolkit`, `TerraformToolkit`, `PulumiToolkit`, and others.
- Every toolkit is assigned to `ToolkitCategory.Development`.
- Each test class follows the same pattern: identity checks, a "not configured" test returning `Skipped`, and a "with config" test returning `Ok`.
- The test scenarios validate correct initialization and minimal execution flow, not real API connectivity.
- Useful as a baseline to ensure new development toolkits adhere to the required interface contract.

## Cross-References
- [[F12]] — The F12 feature set that contains these development tests.
- [[ToolkitCategory]] — Enum used by all development toolkit categories.
- [[AGXIOME]] — The overarching agent framework that hosts these toolkits.

## Source Files
- `AGXIOME.Tests/F12/F12DevelopmentTests.cs` (ingested 2026-05-14)