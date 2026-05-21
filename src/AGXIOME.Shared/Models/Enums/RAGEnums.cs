namespace AGXIOME.Shared.Models;

/// <summary>RAG execution mode: agentic (agent-driven retrieval) or traditional (static pipeline).</summary>
public enum RAGMode { Agentic, Traditional }

/// <summary>Search strategy used in vector/knowledge store queries.</summary>
public enum SearchType { Vector, Keyword, Hybrid }

/// <summary>Logical document type for ingestion and chunking decisions.</summary>
public enum DocumentType { PlainText, Markdown, Code, Pdf, Html, Csv }
