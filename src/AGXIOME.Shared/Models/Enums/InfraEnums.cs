namespace AGXIOME.Shared.Models;

/// <summary>Backend used for agent/session memory persistence.</summary>
public enum MemoryBackend { InMemory, Postgres, MarkdownFiles, Hybrid }

/// <summary>Compression algorithm applied to XLink payloads or stored blobs.</summary>
public enum CompressionLevel { None, Gzip, Brotli }

/// <summary>Strategy for merging outputs from multiple parallel agents.</summary>
public enum MergeStrategy { Concat, Vote, Weighted, SupervisorMerge }
