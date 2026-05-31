$ErrorActionPreference = "Stop"
$sharedDir = "C:\Users\Utente\Desktop\AgnoPortingCSharp\AGXIOMA\src\AGXIOME.Shared"

# ===============================================================
# 1. ILLMProvider.cs
# ===============================================================
$f = "$sharedDir\Interfaces\ILLMProvider.cs"
$c = [System.IO.File]::ReadAllText($f, [System.Text.Encoding]::UTF8)
$c = $c -replace 'public interface ILLMProvider', '/// <summary>LLM provider abstraction. Wraps any language model (OpenAI, Anthropic, Ollama, etc.) with unified completion, streaming, and embedding APIs. Implementations handle auth, retry, rate limiting.</summary>
/// <remarks>All 50+ providers implement this. Use <see cref="ILLMProviderFactory"/> to create instances. Thread-safe.</remarks>
public interface ILLMProvider'
$c = $c -replace 'public interface ILLMProviderFactory', '/// <summary>Factory for creating <see cref="ILLMProvider"/> instances from configuration. Supports custom provider registration.</summary>
public interface ILLMProviderFactory'
[System.IO.File]::WriteAllText($f, $c, [System.Text.Encoding]::UTF8)
Write-Host "1. ILLMProvider.cs done"

# ===============================================================
# 2. ISessionManager.cs
# ===============================================================
$f = "$sharedDir\Interfaces\ISessionManager.cs"
$c = [System.IO.File]::ReadAllText($f, [System.Text.Encoding]::UTF8)
$c = $c -replace 'public interface ISessionManager', '/// <summary>Manages agent session lifecycle: create, persist, resume, and delete sessions with full chat history and mutable session state. Sessions survive across agent restarts.</summary>
/// <remarks>Sessions store conversation history, session state (key-value), and an auto-generated summary. Backed by the configured database provider.</remarks>
public interface ISessionManager'
$c = $c -replace 'public interface ISessionState', '/// <summary>Mutable key-value state store scoped to a single session. Thread-safe. Values are serialized as JSON.</summary>
public interface ISessionState'
[System.IO.File]::WriteAllText($f, $c, [System.Text.Encoding]::UTF8)
Write-Host "2. ISessionManager.cs done"

# ===============================================================
# 3. IStorageInterfaces.cs
# ===============================================================
$f = "$sharedDir\Interfaces\IStorageInterfaces.cs"
$c = [System.IO.File]::ReadAllText($f, [System.Text.Encoding]::UTF8)
$c = $c -replace 'public interface ICommandStore', '/// <summary>CQRS command-side store for creating, updating, and deleting entities. Part of the Unit of Work pattern. One command = one transactional UoW boundary.</summary>
public interface ICommandStore'
$c = $c -replace 'public interface IQueryStore<T> where T : class', '/// <summary>CQRS query-side store for reading entities by ID, specification, or paginated queries. Read-only — never mutates domain state.</summary>
/// <typeparam name="T">The entity type to query.</typeparam>
public interface IQueryStore<T> where T : class'
$c = $c -replace 'public interface ISpecification<T>', '/// <summary>Specification pattern for query-side filtering. Converts to a LINQ expression tree for provider-specific translation.</summary>
/// <typeparam name="T">The entity type.</typeparam>
public interface ISpecification<T>'
$c = $c -replace 'public interface IPaginatedResult<T>', '/// <summary>Paginated query result with total count and page metadata.</summary>
/// <typeparam name="T">The entity type.</typeparam>
public interface IPaginatedResult<T>'
$c = $c -replace 'public interface IUnitOfWork : IDisposable', '/// <summary>Unit of Work for transactional command-side operations. Begin, commit, or rollback a batch of changes atomically. Always dispose when done.</summary>
public interface IUnitOfWork : IDisposable'
[System.IO.File]::WriteAllText($f, $c, [System.Text.Encoding]::UTF8)
Write-Host "3. IStorageInterfaces.cs done"

# ===============================================================
# 4. IToolRegistry.cs
# ===============================================================
$f = "$sharedDir\Interfaces\IToolRegistry.cs"
$c = [System.IO.File]::ReadAllText($f, [System.Text.Encoding]::UTF8)
$c = $c -replace 'public interface IToolRegistry', '/// <summary>Central registry for discovering and resolving tools by name or category. Agents query this to build their function-calling schemas before LLM inference.</summary>
public interface IToolRegistry'
[System.IO.File]::WriteAllText($f, $c, [System.Text.Encoding]::UTF8)
Write-Host "4. IToolRegistry.cs done"

# ===============================================================
# 5. Models/Enums.cs
# ===============================================================
$f = "$sharedDir\Models\Enums.cs"
$c = [System.IO.File]::ReadAllText($f, [System.Text.Encoding]::UTF8)
$c = $c -replace 'public enum HITLRequirementStatus', '/// <summary>Status of a Human-in-the-Loop requirement (pending, approved, rejected, expired).</summary>
public enum HITLRequirementStatus'
$c = $c -replace 'public enum LearningMode', '/// <summary>Learning mode: Always (auto-save after every run), Agentic (agent decides via tool), Propose (agent proposes, user confirms).</summary>
public enum LearningMode'
$c = $c -replace 'public enum LearningSessionMode', '/// <summary>Session context mode: Summary (summarize), Planning (track goals).</summary>
public enum LearningSessionMode'
$c = $c -replace 'public enum EntityMemoryType', '/// <summary>Type of entity memory: Fact, Event, or Relationship.</summary>
public enum EntityMemoryType'
[System.IO.File]::WriteAllText($f, $c, [System.Text.Encoding]::UTF8)
Write-Host "5. Enums.cs done"

# ===============================================================
# 6. Models/LLMWIKYRecords.cs
# ===============================================================
$f = "$sharedDir\Models\LLMWIKYRecords.cs"
$c = [System.IO.File]::ReadAllText($f, [System.Text.Encoding]::UTF8)
if ($c -notmatch '/// <summary>') {
    $c = '/// <summary>LLM completion request: model, messages, optional tools, temperature, max tokens.</summary>' + "`r`n" + $c
}
[System.IO.File]::WriteAllText($f, $c, [System.Text.Encoding]::UTF8)
Write-Host "6. LLMWIKYRecords.cs done"

# ===============================================================
# 7. Models/ProviderEnums.cs (large file - add docs to key enums)
# ===============================================================
$f = "$sharedDir\Models\ProviderEnums.cs"
$c = [System.IO.File]::ReadAllText($f, [System.Text.Encoding]::UTF8)
$c = $c -replace 'public enum AgentStatus', '/// <summary>Agent execution status: idle, running, completed, failed, paused, cancelled.</summary>
public enum AgentStatus'
$c = $c -replace 'public enum OrchestratorTaskStatus', '/// <summary>Orchestrator task status: pending, in-progress, completed, failed, cancelled, waiting-for-hitl.</summary>
public enum OrchestratorTaskStatus'
$c = $c -replace 'public enum PlanState', '/// <summary>Orchestration plan state: created, decomposing, executing, completed, failed, cancelled.</summary>
public enum PlanState'
[System.IO.File]::WriteAllText($f, $c, [System.Text.Encoding]::UTF8)
Write-Host "7. ProviderEnums.cs done"

# ===============================================================
# 8. Models/MiscRecords.cs
# ===============================================================
$f = "$sharedDir\Models\MiscRecords.cs"
$c = [System.IO.File]::ReadAllText($f, [System.Text.Encoding]::UTF8)
if ($c -notmatch '/// <summary>') {
    $c = '/// <summary>Miscellaneous shared records: XLink envelope/header/options, Guardrail/Hook/Approval contexts and results, Audit events.</summary>' + "`r`n" + $c
}
[System.IO.File]::WriteAllText($f, $c, [System.Text.Encoding]::UTF8)
Write-Host "8. MiscRecords.cs done"

# ===============================================================
# 9-14. Remaining fast model files
# ===============================================================
$additions = @{
    "SessionTeamWorkflowRecords.cs" = '/// <summary>Session, team, and workflow records: Session with history, TeamContext/Response, WorkflowContext, StepResult/Type, WorkflowResult.</summary>'
    "SessionType.cs" = '/// <summary>Defines the types of sessions an agent can participate in.</summary>'
    "TeamMode.cs" = '/// <summary>Defines how a team executes: Coordinate, Route, or Broadcast.</summary>'
    "ToolRecords.cs" = '/// <summary>Tool-related records: ToolContext (inputs, agent, session), ToolResult (success/failure with data), ToolCallLimit, ToolResultCache.</summary>'
    "WorkflowStatus.cs" = '/// <summary>Defines the possible states of a workflow: Draft, Running, Paused, Completed, Failed.</summary>'
}

foreach ($kv in $additions.GetEnumerator()) {
    $f = "$sharedDir\Models\$($kv.Key)"
    if (Test-Path $f) {
        $c = [System.IO.File]::ReadAllText($f, [System.Text.Encoding]::UTF8)
        if ($c -notmatch '/// <summary>') {
            $c = $kv.Value + "`r`n" + $c
        }
        [System.IO.File]::WriteAllText($f, $c, [System.Text.Encoding]::UTF8)
        Write-Host "  $($kv.Key) done"
    }
}

Write-Host "`n=== ALL DOCS COMPLETE ==="