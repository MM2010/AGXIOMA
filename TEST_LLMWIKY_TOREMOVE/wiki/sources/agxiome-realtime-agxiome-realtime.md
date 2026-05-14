---
title: Agxiome Realtime
category: source
tags: [realtime, signalr, orchestration, dotnet10]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# Agxiome Realtime

## Summary
The `AGXIOME.Realtime` project provides real-time notification capabilities to the AGXIOME agent framework using ASP.NET Core SignalR. It acts as a bridge between the orchestration layer (`AGXIOME.Orchestration`) and connected clients, delivering completion and status events without the overhead of polling. By relying on the SignalR implementation built into .NET 10 (`Microsoft.AspNetCore.App`), it avoids the legacy `Microsoft.AspNetCore.SignalR` 1.x package and its associated Newtonsoft.Json vulnerability (CVE).

## Key Details
- Targets `net10.0` with `ImplicitUsings` and `Nullable` enabled.
- References three core library projects: `AGXIOME.Shared`, `AGXIOME.Core`, and `AGXIOME.Orchestration`.
- Uses `FrameworkReference` to `Microsoft.AspNetCore.App` (includes SignalR natively in .NET 10) instead of the deprecated `Microsoft.AspNetCore.SignalR` NuGet package.
- Houses the `SignalRCompletionNotifier` class that bridges orchestration events to SignalR hubs.
- Eliminates a known CVE by removing the old Newtonsoft.Json-dependent SignalR package.

## Cross-References
- [[sources/agxiome-orchestration]] — provides the orchestration events consumed by this real-time notifier.
- [[sources/agxiome-core]] — core agent abstractions used by the real-time layer.
- [[sources/agxiome-shared]] — shared utilities and data contracts.
- [[concepts/realtime-messaging]] — architectural pattern for real-time agent updates.

## Source Files
- `AGXIOME.Realtime/AGXIOME.Realtime.csproj` (ingested 2026-05-14)