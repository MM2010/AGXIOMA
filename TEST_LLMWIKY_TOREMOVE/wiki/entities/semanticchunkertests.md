---
title: SemanticChunkerTests
category: entity
tags: [tests, chunker, semantic-chunker, unit-tests]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# SemanticChunkerTests

## Summary
The `SemanticChunkerTests` class validates the behavior of the `SemanticChunker` component in the AGXIOME framework. It covers chunker type identification, empty input handling, fallback to syntactic splitting when no embedder is provided, boundary insertion based on low cosine similarity using a mock embedder, and correctness of the cosine similarity calculation. These tests ensure that the semantic chunker behaves correctly both synchronously and asynchronously, and that its embedder-driven boundary detection works as expected.

## Key Details
- Contains 5 unit tests (xUnit) for the `SemanticChunker` class.
- Uses NSubstitute to create a deterministic mock `IEmbedder` that returns orthogonal vectors at configurable sentence indices to force semantic boundaries.
- Test `ChunkerType_IsSemantic` verifies that `ChunkerType` property returns `ChunkerType.Semantic`.
- Test `EmptyInput_ReturnsSingleEmptyChunk` confirms that an empty string produces a single chunk with empty content.
- Test `WithoutEmbedder_FallsBackToSyntacticSplit` checks that without an `IEmbedder`, the chunker falls back to syntactic splitting (boundaries every `maxTokens` sentences).
- Test `WithEmbedder_InsertsBoundaryAtLowSimilarity` ensures that when an embedder returns an orthogonal vector at a specific index, a semantic boundary is inserted, producing at least two chunks.
- Test `CosineSimilarity_KnownValues` validates the static `CosineSimilarity` method with identical vectors (result 1.0), orthogonal (0.0), and zero vector (0.0 without exception).
- All tests use a default `maxTokens` of 200 and no overlap unless otherwise specified.

## Cross-References
- [[SemanticChunker]] — the class under test
- [[IEmbedder]] — interface used to generate embeddings
- [[ChunkerType]] — enum returned by `ChunkerType` property

## Source Files
- `AGXIOME.Tests/SemanticChunkerTests.cs` (ingested 2026-05-14)