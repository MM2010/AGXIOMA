---
title: F12 Enterprise Toolkit Tests
category: entity
tags: [enterprise, toolkit, tests, unit tests, f12, salesforce, hubspot, zendesk, servicenow, datadog, pagerduty, sentry, grafana, newrelic]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# F12 Enterprise Toolkit Tests

## Summary
This suite of unit tests validates the core contract of all enterprise toolkits in the AGXIOMA framework, including Salesforce, HubSpot, Zendesk, ServiceNow, Datadog, PagerDuty, Sentry, Grafana, and NewRelic. Each test verifies that the toolkit exposes the correct identifier (`Id`), display name (`Name`), and category (`ToolkitCategory.Enterprise`), that an unconfigured instance returns a `Skipped` status when executed, and that a configured instance returns `Ok`. These tests ensure consistent behavior across all enterprise integrations.

## Key Details
- **File**: `AGXIOME.Tests/F12/F12EnterpriseTests.cs`
- **Namespace**: `AGXIOME.Tests.F12`
- **Test classes**: `SalesforceToolkitTests`, `HubSpotToolkitTests`, `ZendeskToolkitTests`, `ServiceNowToolkitTests`, `DatadogToolkitTests`, `PagerDutyToolkitTests`, `SentryToolkitTests`, `GrafanaToolkitTests`, `NewRelicToolkitTests` (and others truncated)
- **Common test pattern**: Each class contains four facts:
  - `Id_IsCorrect` – asserts the toolkit's `Id` property (e.g., `"salesforce"`)
  - `Name_IsCorrect` – asserts the `Name` property (e.g., `"Salesforce"`)
  - `Category_IsCorrect` – asserts `Category == ToolkitCategory.Enterprise`
  - `NotConfigured_ReturnsSkipped` – calls `ExecuteAsync` with no configuration and checks `ToolkitResultStatus.Skipped`
  - `Execute_WithConfig_ReturnsOk` – creates a `ToolkitConfig` with an `api_key` and calls `ExecuteAsync` with sample parameters, expecting `ToolkitResultStatus.Ok`
- **Sample actions tested**: `query`, `list_contacts`, `list_tickets`, `list_incidents`, `query_metrics`, `list_issues`, `list_dashboards`, etc.
- **Category**: All toolkits are enterprise-level integrations.

## Cross-References
- [[entities/enterprisetoolkits]] — the collection of enterprise toolkits tested here
- [[concepts/toolkitcategory]] — defines the `Enterprise` category used in these tests
- [[concepts/toolkitresultstatus]] — explains `Skipped` and `Ok` statuses

## Source Files
- `AGXIOME.Tests/F12/F12EnterpriseTests.cs` (ingested 2026-05-14)