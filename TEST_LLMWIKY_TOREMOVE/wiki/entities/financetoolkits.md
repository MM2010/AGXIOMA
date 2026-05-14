---
title: Finance Toolkit Group
category: entity
tags: [finance, toolkit, api, aggregation]
source_count: 5
created_at: 2026-05-14
updated_at: 2026-05-14
---

# Finance Toolkit Group

## Summary
The `AGXIOME.Toolkits.Finance` namespace bundles a collection of ready‑to‑use agent toolkits that wrap popular financial data APIs. Each toolkit exposes a uniform interface via `ExecuteCoreAsync` with action‑based commands (e.g., `quote`, `history`, `search`), sharing a common HTTP client and returning `ToolkitResult`. The group covers equities (Yahoo Finance, Alpha Vantage, Polygon.io), cryptocurrencies (CoinGecko), and forex/commodities (Alpha Vantage), enabling agents to fetch real‑time and historical market data with minimal configuration.

## Key Details
- Five toolkits implemented: `YahooFinanceToolkit`, `AlphaVantageToolkit`, `PolygonToolkit`, `CoinGeckoToolkit`, and additional ones (from truncated source).
- All inherit from `ToolkitBase` and override `ExecuteCoreAsync` with a `switch` on the `action` parameter.
- A static, internal `Http.Client` (30‑second timeout, `Mozilla/5.0` user‑agent) is reused across all toolkits.
- `IsConfigured` varies: Yahoo Finance and CoinGecko always `true`; Alpha Vantage and Polygon require a non‑empty `api_key` setting.
- Action names are case‑sensitive and specific to each provider; invalid actions return `ToolkitResult.Fail` with a list of valid actions.
- Responses are deserialized as `JsonElement` and wrapped in `ToolkitResult.Success`; Alpha Vantage additionally checks for rate‑limit (`Note`) and error messages.

## Cross-References
- [[ToolkitBase]] — base class defining the lifecycle and configuration pattern.
- [[ToolkitExecutionPipeline]] — how toolkits are discovered, loaded, and executed within the agent.

## Source Files
- `AGXIOME.Toolkits/Finance/FinanceToolkits.cs` (ingested 2026-05-14)