# Installation

## NuGet Packages

```bash
# Core framework (pulls Shared automatically)
dotnet add package AGXIOME.Core --prerelease

# Specific packages
dotnet add package AGXIOME.LLMWIKY --prerelease      # LLM inference (50+ providers)
dotnet add package AGXIOME.Storage --prerelease       # CQRS + vector DBs + chunkers
dotnet add package AGXIOME.Orchestration --prerelease # Multi-agent orchestrator
dotnet add package AGXIOME.Toolkits --prerelease      # 161 prebuilt integrations
dotnet add package AGXIOME.Security --prerelease      # Crypto, RBAC, guardrails
dotnet add package AGXIOME.Realtime --prerelease      # SignalR hubs + backplane
dotnet add package AGXIOME.Embedders --prerelease     # 25 embedding providers
dotnet add package AGXIOME.Readers --prerelease       # 28 document readers
dotnet add package AGXIOME.XLink --prerelease         # Typed transport protocol
dotnet add package AGXIOME.Telemetry --prerelease     # OpenTelemetry metrics
dotnet add package AGXIOME.CLI --prerelease           # Command-line tool
```

## Package Dependency Tree

```
AGXIOME.Core
├── AGXIOME.Shared          (always included)
├── AGXIOME.LLMWIKY         (LLM inference)
├── AGXIOME.Orchestration   (multi-agent orchestrator)
├── AGXIOME.Security        (crypto, RBAC, guardrails)
├── AGXIOME.Storage         (CQRS + vector DBs)
├── AGXIOME.Realtime        (SignalR)
├── AGXIOME.XLink           (transport)
└── AGXIOME.Telemetry       (observability)

Packages you can add individually:
├── AGXIOME.Toolkits        (161 prebuilt integrations)
├── AGXIOME.Embedders       (25 embedding providers)
├── AGXIOME.Readers         (28 document readers)
└── AGXIOME.CLI             (command-line tool)
```

## Prerequisites

- **.NET 10 SDK** ([download](https://dotnet.microsoft.com/download/dotnet/10.0))
- Optional: Docker (for Testcontainers-based integration tests)

## Verify Installation

```bash
dotnet list package
```

Should show `AGXIOME.Core 0.0.1-beta.1` or higher.

## Next

- [Quick Start](quickstart.md)
- [Configuration](configuration.md)
