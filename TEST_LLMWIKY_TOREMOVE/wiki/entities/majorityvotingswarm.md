---
title: MajorityVotingSwarm
category: entity
tags: [voting, swarm, majority, consensus, pattern]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# MajorityVotingSwarm

## Summary
The `MajorityVotingSwarm` is a core consensus pattern in AGXIOMA that orchestrates multiple agents to independently answer a question, then selects a winner using one of three voting modes: simple count, weighted score, or LLM judge. It includes optional escalation policies for low‑confidence outcomes, agent weighting by reputation/specialization, and an audit callback. This ensures robust decision‑making even in the presence of disagreement or errors.

## Key Details
- **Minimum agents**: 2 (throws `ArgumentException` otherwise).
- **Voting modes** (`VotingMode`):
  - `SimpleCount` – most frequent normalized answer wins.
  - `WeightedScore` – weighted by per‑agent `AgentWeights` dictionary.
  - `LLMJudge` – a dedicated judge agent evaluates all answers and selects the best, providing a confidence score.
- **Disagreement policies** (`DisagreementPolicy`):
  - `Abstain` – returns result even if confidence is low.
  - `ForceBest` – returns the current winner regardless.
  - `RequestRevote` – repeats voting with a refined question (notes previous low consensus).
- **`ConfidenceThreshold`** (default 0.5) triggers revote or other policy.
- **`AgentWeights`**: `Dictionary<string, double>` keys on agent ID.
- **`OnVoteComplete`**: action callback receives `VotingResult` for audit trails.
- Parallel execution via `Task.WhenAll` – all agents vote concurrently.
- Failed agents yield `[ERROR: ...]` responses with weight 0.
- Voting rounds can be nested (`RevoteRound` flag in result).
- **Normalization**: answers are trimmed, lowercased, and truncated to 200 characters for grouping.

## Cross-References
- [[IAgxiomeAgent]] — the agent interface used by the swarm.
- [[VotingResult]] — the output record containing winner, all votes, confidence, duration, and winner agent ID.
- [[AgentRequest]] — the input request model passed to each agent.
- [[MajorityVotingConfig]] — configuration record for voting mode, policy, weights, threshold, and callback.

## Source Files
- `AGXIOME.Core/Patterns/MajorityVotingSwarm.cs` (ingested 2026-05-14)