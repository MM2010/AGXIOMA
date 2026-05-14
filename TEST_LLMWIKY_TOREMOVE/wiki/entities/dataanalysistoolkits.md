---
title: Data Analysis Toolkits
category: entity
tags: [data-analysis, toolkit, sql, sqlite, pandas-like, postgresql, mysql, duckdb, clickhouse, spark]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# Data Analysis Toolkits

## Summary
The Data Analysis Toolkits provide a unified interface for querying and manipulating data across multiple backends. Implemented as `ToolkitBase` derivatives, they expose SQL, in-memory tabular operations (Pandas-like), and database-specific connectors. While several toolkits are stubs that require external NuGet packages (PostgreSQL, MySQL, DuckDB, ClickHouse, Spark), SQLite and Pandas-like are fully functional out‑of‑the‑box using only the BCL. A generic SQL toolkit exists as a dispatcher to these provider‑specific implementations.

## Key Details
- **Generic SQL (T-34)**: Delegates to provider toolkits; returns a failure message instructing users to use specific toolkits with a `connection_string`.
- **DuckDB (T-35)**: Stub requiring `DuckDB.NET.Data` package.
- **PostgreSQL (T-36)**: Stub requiring `Npgsql` package; configured via `connection_string`.
- **MySQL (T-37)**: Stub requiring `MySql.Data` or `MySqlConnector`; configured via `connection_string`.
- **SQLite (T-38)**: Fully functional using `Microsoft.Data.Sqlite`; supports in‑memory and file‑based databases; can run DDL, DML, and SELECT queries.
- **ClickHouse (T-39)**: Stub requiring `ClickHouse.Client`.
- **Apache Spark (T-40)**: Stub requiring `Microsoft.Spark` and a running Spark cluster.
- **Pandas‑like (T-41)**: Pure BCL `DataTable` with actions: `load_json`, `head`, `describe`, `filter`, `drop`, `list_tables`; zero external dependencies.
- All toolkits inherit from `ToolkitBase` and expose `Id`, `Name`, `Category`, and `IsConfigured` overrides.
- The `IsConfigured` property returns `true` for SQLite and Pandas‑like; for others it depends on presence of a `connection_string` setting.

## Cross-References
- [[Toolkitbasics]] — Foundation for all toolkits, including configuration and execution pattern.
- [[Datasourcetoolkits]] — Related category for non‑analysis data sources (e.g., file loaders).
- [[Datatransformationtoolkits]] — Complementary toolkits for data wrangling.

## Source Files
- `AGXIOME.Toolkits/DataAnalysis/DataAnalysisToolkits.cs` (ingested 2026-05-14)