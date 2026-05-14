---
title: F12 Finance Toolkit Tests
category: entity
tags: [finance, tests, toolkits, unit-tests]
source_count: 9
created_at: 2026-05-14
updated_at: 2026-05-14
---

# F12 Finance Toolkit Tests

## Summary
This test suite validates the identity, configuration, and basic execution behavior of finance-related toolkits in the AGXIOME .NET agent framework. Each test class covers a specific toolkit (Yahoo Finance, Alpha Vantage, Polygon.io, CoinGecko, Binance, Stripe, Plaid, QuickBooks, Xero, and others) by checking its `Id`, `Name`, `Category` (all must be `Financial`), and the result status when executed without configuration versus with a minimal configuration.

## Key Details
- Nine explicit test classes are present in the snippet: `YahooFinanceToolkitTests`, `AlphaVantageToolkitTests`, `PolygonToolkitTests`, `CoinGeckoToolkitTests`, `BinanceToolkitTests`, `StripeToolkitTests`, `PlaidToolkitTests`, `QuickBooksToolkitTests`, and `XeroToolkitTests` (additional classes likely follow).
- Each toolkit must have a unique `Id` (e.g., `"yahoo_finance"`), a `Name` (e.g., `"Yahoo Finance"`), and a `Category` of `ToolkitCategory.Finance`.
- When no configuration is provided, most toolkits return `ToolkitResultStatus.Skipped`; exceptions are Yahoo Finance and CoinGecko, which return `ToolkitResultStatus.Ok`.
- All toolkits return `ToolkitResultStatus.Ok` when invoked with a valid `ToolkitConfig` containing an `api_key`.
- The tests use `xUnit` and `Fact` attributes; all methods are `async Task`.
- The source file is located in the `AGXIOME.Tests/F12/` folder, suggesting these are part of a broader F12 feature or module.

## Cross-References
- [[entities/f12]] — the parent F12 module that groups these finance toolkit tests
- [[entities/yahoo-finance-toolkit]] — the corresponding toolkit implementation
- [[entities/alpha-vantage-toolkit]] — the corresponding toolkit implementation
- [[entities/polygon-toolkit]] — the corresponding toolkit implementation
- [[entities/coingecko-toolkit]] — the corresponding toolkit implementation
- [[entities/binance-toolkit]] — the corresponding toolkit implementation
- [[entities/stripe-toolkit]] — the corresponding toolkit implementation
- [[entities/plaid-toolkit]] — the corresponding toolkit implementation
- [[entities/quickbooks-toolkit]] — the corresponding toolkit implementation
- [[entities/xero-toolkit]] — the corresponding toolkit implementation

## Source Files
- `AGXIOME.Tests/F12/F12FinanceTests.cs` (ingested 2026-05-14)