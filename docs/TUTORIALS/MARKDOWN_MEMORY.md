# Markdown Memory — RAG with Markdown Files

AGXIOME's `MarkdownMemoryStore` saves agent interactions as Markdown files with YAML frontmatter, enabling full-text search and optional KnowledgeBase semantic search.

## Configuration

```csharp
// In agent config
var config = new AgentConfig
{
    Persistence = new StorageConfig
    {
        MemoryBackend = MemoryBackend.MarkdownFiles,
        MarkdownMemory = new MarkdownMemoryConfig
        {
            BasePath = "./agent-memory",
            UseKnowledgeBase = true  // Enables semantic search
        }
    }
};
```

## CLI Commands

```bash
# Initialize a memory store
agxiome memory init ./my-memory

# Add a markdown file
agxiome memory add ./notes.md

# Search memory
agxiome memory search "query text" --dir ./my-memory
```

## Programmatic Usage

```csharp
using AGXIOME.Storage.Knowledge;
using AGXIOME.Shared.Config;
using AGXIOME.Shared.Models;

var store = new MarkdownMemoryStore(new MarkdownMemoryConfig
{
    BasePath = "./agent-memory"
});

// Save an interaction
await store.SaveInteractionAsync(new AgentInteraction(
    AgentId: "assistant-1",
    Role: "assistant",
    Content: "The answer is 42."
));

// Load all memory
var memories = await store.LoadMarkdownAsync();

// Full-text search
var matches = await store.SearchByGrepAsync("answer");
```

## YAML Frontmatter Format

Each memory file is stored as:

```markdown
---
agent: assistant-1
role: assistant
created_at: 2026-05-13T12:00:00.0000000Z
tool_calls: 
---

The answer is 42.
```
