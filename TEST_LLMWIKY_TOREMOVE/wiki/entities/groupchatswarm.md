---
title: GroupChatSwarm
category: entity
tags: [groupchat, multi-agent, speaker-selection, pattern, realtime]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# GroupChatSwarm

## Summary
`GroupChatSwarm` is a multi-agent conversation orchestrator that manages turn-taking among a set of agents with advanced speaker selection policies. It supports round‑robin, random, priority‑weighted, mention‑driven, and custom selectors; prevents agent monopolisation via `MaxConsecutiveSpeaker`; and integrates with realtime push via an `OnTurn` callback. The class produces a full typed conversation transcript and an optional summary from a designated summariser agent. It is designed for structured, scalable group discussions within the AGXIOMA framework.

## Key Details
- Requires **at least two agents** (`IAgxiomeAgent`); throws `ArgumentException` if fewer.
- Speaker selection is governed by a `SpeakerPolicy` enum: `RoundRobin`, `Random`, `Priority`, `MentionDriven`, `Custom`.
- `@mention` parsing (regex `@(\w[\w\-]*)`) allows any agent to explicitly hand off to another; detected mentions override the next speaker.
- `PriorityWeights` dictionary influences selection frequency under `Priority` policy.
- `ConversationFilter` (implied by `MaxConsecutiveSpeaker`) skips agents that exceed consecutive turn limit.
- `MaxConsecutiveSpeaker` (default config) prevents a single agent from dominating.
- Optional `CustomSpeakerSelector` delegate for fully custom turn scheduling.
- `OnTurn` callback is invoked after each agent's turn, enabling realtime integration (e.g. SignalR).
- Conversation terminates after `MaxLoops` turns, or when an agent includes `[GROUPCHAT_END]` in its response.
- `ContinueOnAgentError` config determines whether to stop or log errors and continue.
- Returns a `GroupChatResult` with `Topic`, `Transcript` (list of `ChatTurn`), optional `Summary` (from a summariser agent), `Duration`, and `TotalTurns`.

## Cross-References
- [[IAgxiomeAgent]] — the agent interface used by GroupChatSwarm.
- [[GroupChatConfig]] — configuration object for policies, weights, and callbacks.
- [[GroupChatResult]] — return type containing transcript and summary.
- [[AgentResponse]] — result from a single agent invocation.
- [[ChatTurn]] — data structure representing one turn in the conversation.

## Source Files
- `AGXIOME.Core/Patterns/GroupChatSwarm.cs` (ingested 2026-05-14)