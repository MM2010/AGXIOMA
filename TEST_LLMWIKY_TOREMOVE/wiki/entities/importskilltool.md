---
title: ImportSkillTool
category: entity
tags: [tool, dynamic, skills, import, marketplace]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# ImportSkillTool

## Summary
The `ImportSkillTool` is an `AgxiomeFunction` that orchestrates the import of a skill from the external Skills.sh marketplace. It validates the user and skill identifiers, fetches the skill, attempts an adaptation (compilation/validation) that may produce warnings, and then registers the skill as `Pending` in the local registry. The tool returns a JSON result with skill metadata and an optional adaptation warning.

## Key Details
- **Constructor dependencies**: `SkillsShClient`, `SkillsShRegistry`, `SkillAdapter`.
- **`Name`**: `"import_skill"`
- **`Description`**: "Imports a skill from the Skills.sh marketplace for the specified user. The skill is saved as Pending until trusted."
- **`Categories`**: `["dynamic", "skills", "import"]`
- **`ExecuteAsync`**:
  - Requires `userId` and `skillId` in `context.Parameters`.
  - Calls `skillsClient.GetSkillAsync` to retrieve the skill from the marketplace.
  - Runs `skillAdapter.AdaptAsync` in a try-catch (non-fatal – adaptation errors become warnings).
  - Calls `skillsRegistry.ImportAsync` to persist the skill with status `Pending`.
  - Returns a `ToolResult` with JSON output containing `Id`, `Name`, `Type`, `Status = "Pending"`, and optional `AdaptWarning`.
- **`GetParam` helper**: Extracts string value from parameters dictionary.
- **Error handling**: Returns `ToolResult(false)` for missing parameters or if the skill is not found in the marketplace.

## Cross-References
- [[SkillsShClient]] — Used to fetch skills from the marketplace.
- [[SkillsShRegistry]] — Used to import/register the skill locally.
- [[SkillAdapter]] — Adapts/validates the skill; errors become non-fatal warnings.
- [[AgxiomeFunction]] — Base class for all tools in the framework.
- [[ToolResult]] — Return type for tool execution results.
- [[ToolContext]] — Provides parameters and execution context.

## Source Files
- `AGXIOME.Core/Tools/ImportSkillTool.cs` (ingested 2026-05-14)