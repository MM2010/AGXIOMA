// ─────────────────────────────────────────────────────────────────────────────
// All provider interfaces and their companion records have been split into
// individual files under Interfaces/.
//
// IEmbedder                                   → Interfaces/IEmbedder.cs
// IChunker + DocumentChunk                    → Interfaces/IChunker.cs
// IReranker + RerankCandidate + RerankResult  → Interfaces/IReranker.cs
// IVectorStore + VectorRecord + VectorFilter
//   + VectorFilterOperator + VectorSearchResult → Interfaces/IVectorStore.cs
// IDocumentReader + DocumentSection
//   + DocumentContent                         → Interfaces/IDocumentReader.cs
// IToolkit + ToolkitResult + ToolkitToolDescriptor → Interfaces/IToolkit.cs
// IDbProvider + DbPage<T>                     → Interfaces/IDbProvider.cs
// ─────────────────────────────────────────────────────────────────────────────

