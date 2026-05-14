---
title: F12 Data Analysis Tests
category: entity
tags: [tests, data-analysis, toolkits, unit-tests]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# F12 Data Analysis Tests

## Summary
The `F12DataAnalysisTests.cs` file contains unit tests for all data analysis toolkits in the AGXIOMA framework. Each toolkit test class verifies that the toolkit's identifier, name, and category are correctly set, that an unconfigured toolkit returns a `Skipped` status, and that a configured toolkit with valid input returns `Ok`. This ensures consistent metadata and basic execution flow across the data analysis category.

## Key Details
- **Namespace:** `AGXIOME.Tests.F12`
- **Tested toolkits:** Sql, DuckDb, PostgreSQL, MySQL, SQLite, ClickHouse, Spark, Pandas-like, and Excel.
- Each test class contains 4–5 test methods:
  - `Id_IsCorrect` – validates the `Id` property.
  - `Name_IsCorrect` – validates the `Name` property.
  - `Category_IsCorrect` – asserts `ToolkitCategory.DataAnalysis`.
  - `NotConfigured_ReturnsSkipped` – calls `ExecuteAsync` without config and expects `ToolkitResultStatus.Skipped`.
  - `Execute_WithConfig_ReturnsOk` – calls `ExecuteAsync` with a `ToolkitConfig` (using dummy API key) and example parameters (e.g., `sql: "SELECT 1"`) and expects `ToolkitResultStatus.Ok`.
- All tests are synchronous `async Task` methods using xUnit `[Fact]`.
- The Spark toolkit uses a job-focused action (`run_job`) and parameters (`script`), while Pandas-like uses `query` and `expression`.

## Cross-References
- [[SqlToolkit]] — the SQL toolkit under test
- [[DuckDbToolkit]] — the DuckDB toolkit under test
- [[PostgreSqlToolkit]] — the PostgreSQL toolkit under test
- [[MySqlToolkit]] — the MySQL toolkit under test
- [[SqliteToolkit]] — the SQLite toolkit under test
- [[ClickHouseToolkit]] — the ClickHouse toolkit under test
- [[SparkToolkit]] — the Apache Spark toolkit under test
- [[PandasLikeToolkit]] — the Pandas-like toolkit under test
- [[ExcelToolkit]] — the Excel toolkit under test
- [[ToolkitCategory]] — the category enumeration used by all data analysis toolkits
- [[ToolkitResultStatus]] — the result status enumeration used in test assertions

## Source Files
- `AGXIOME.Tests/F12/F12DataAnalysisTests.cs` (ingested 2026-05-14)