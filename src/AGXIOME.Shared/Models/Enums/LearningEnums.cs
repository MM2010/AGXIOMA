namespace AGXIOME.Shared.Models;

/// <summary>Controls when the agent proactively learns and persists knowledge.</summary>
public enum LearningMode { Always, Agentic, Propose }

/// <summary>Mode for session-level learning context: full summary or planning-focused.</summary>
public enum LearningSessionMode { Summary, Planning }

/// <summary>Category of entity memory entries stored in the knowledge graph.</summary>
public enum EntityMemoryType { Fact, Event, Relationship }
