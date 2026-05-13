---
goal: Dynamic Agent Runtime — Hot-folder .cs drop + LLM code generation + lazy Roslyn compile + Skills.sh
version: 2.0
date_created: 2026-05-13
last_updated: 2026-05-13
owner: AGXIOMA Core Team
status: 'Completed'
tags: [feature, architecture, runtime-compilation, roslyn, skills, extensibility, hot-folder, lazy-load]
---

# Introduction

![Status: Completed](https://img.shields.io/badge/status-Completed-brightgreen)

Il **Dynamic Agent Runtime** è un sistema a tre vettori che permette di espandere AGXIOMA senza mai riavviarlo:

**Vettore 1 — Hot-Folder Drop**
L'utente copia un file `.cs` in una cartella dedicata (`~/.agxioma/agents/{userId}/src/`). Il sistema lo rileva istantaneamente via `FileSystemWatcher`, lo legge in sola lettura (estrae metadati — nome, descrizione, attributi), e lo rende **visibile** nel catalogo agenti dell'utente. L'assembly **non viene compilato subito**: viene compilato con Roslyn **solo al primo utilizzo** (lazy compilation). Questo rende il catalogo istantaneo e la RAM sempre contenuta.

**Vettore 2 — LLM Code Generation**
L'orchestratore riceve una richiesta in linguaggio naturale, genera codice C# via LLM, lo valida con analisi statica Roslyn, lo compila e lo registra — tutto in < 3 secondi, senza downtime.

**Vettore 3 — Skills.sh Integration**
Skill HTTP/API esterne importate dal marketplace Skills.sh, adattate all'interfaccia `IAgxiomeTool`, disponibili immediatamente senza compilazione.

**Differenziatore assoluto** rispetto a Swarms, AutoGen, CrewAI:
- Python non ha Roslyn compiler-as-a-service
- `AssemblyLoadContext` con `IsCollectible=true` consente hot-reload/unload con GC corretto
- Hot-folder + lazy compile = catalogo infinito a zero costo RAM finché non serve

---

## 1. Requirements & Constraints

- **REQ-001**: Il sistema DEVE monitorare in tempo reale la cartella `~/.agxioma/agents/{userId}/src/` tramite `FileSystemWatcher`
- **REQ-002**: I file `.cs` rilevati DEVONO essere registrati nel catalogo come `DiscoveredAgentEntry` (solo metadati) **senza compilazione immediata**
- **REQ-003**: La compilazione DEVE avvenire solo al primo `RunAsync()` sull'agente (lazy) e il risultato DEVE essere cachato
- **REQ-004**: L'utente DEVE poter vedere la lista degli agenti disponibili (inclusi quelli non ancora compilati) tramite REST API `GET /agents/user/{userId}`
- **REQ-005**: Il sistema DEVE poter compilare codice C# a runtime da stringa (Roslyn `CSharpCompilation`) senza riavvio del processo
- **REQ-006**: Gli assembly compilati DEVONO essere isolati via `AssemblyLoadContext` con `IsCollectible=true`
- **REQ-007**: L'orchestratore DEVE poter generare codice C# via LLM a partire da descrizione naturale
- **REQ-008**: Il codice (sia drop-folder che LLM-generated) DEVE implementare `ISimpleDynamicAgent` o avere attributo `[AgxiomeAgent]`
- **REQ-009**: Il codice DEVE essere validato staticamente (Roslyn SyntaxTree) prima della compilazione
- **REQ-010**: Le customizzazioni DEVONO essere persistite: sorgente `.cs` su disco + assembly `.dll` precompilato in cache (`~/.agxioma/agents/{userId}/bin/`)
- **REQ-011**: Al riavvio, se il `.dll` cache è presente e il `.cs` non è cambiato (hash SHA256), DEVE essere caricato direttamente senza ricompilare
- **REQ-012**: Il sistema DEVE supportare import da Skills.sh tramite HTTP API
- **SEC-001**: Il codice user-drop DEVE essere eseguito in `AssemblyLoadContext` isolato — no accesso FS dell'host, no reflection su tipi sensibili
- **SEC-002**: Analisi statica obbligatoria: blocca `System.IO`, `System.Reflection`, `System.Diagnostics.Process`, `unsafe`, `DllImport`, `Marshal`
- **SEC-003**: Allowlist di assembly referenziabili dal codice utente (solo: `AGXIOME.Shared`, `System.Runtime`, `System.Collections`, `System.Text.Json`, `System.Net.Http`)
- **SEC-004**: Skills.sh skills passano per whitelist + review prima di essere marcate `Trusted`
- **CON-001**: Solo .NET 10 — zero dipendenza su MSBuild o `dotnet` CLI esterno
- **CON-002**: Compilazione Roslyn (warm) < 500ms per file < 200 righe
- **CON-003**: Il `FileSystemWatcher` DEVE essere debounced (300ms) per evitare eventi multipli su salvataggio editor
- **GUD-001**: Il codice generato/dropped DEVE avere attributo `[AgxiomeAgent(Id, Description)]` per metadata extraction senza eseguire codice
- **PAT-001**: Pattern `WeakReference<AgentAssemblyContext>` per GC corretto degli assembly unloaded
- **PAT-002**: Pattern `Lazy<Task<CompiledAgent>>` per lazy compilation thread-safe

---

## 2. Implementation Steps

### Phase 1 — Interfaccia, Attributo e Template

- GOAL-001: Contratto minimo per ogni agente user-drop o LLM-generated

| Task     | Description                                                                                                                                                                                 | Completed | Date |
| -------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------- | ---- |
| TASK-001 | Creare progetto `AGXIOME.DynamicRuntime.csproj` con ref a `AGXIOME.Shared` + `AGXIOME.LLMWIKY`. Aggiungere `Microsoft.CodeAnalysis.CSharp 4.*`                                             |           |      |
| TASK-002 | Creare `Interfaces/ISimpleDynamicAgent.cs` — `string AgentId`, `string Description`, `Task<string> RunAsync(string input, CancellationToken ct)`. Zero dipendenze da Shared               |           |      |
| TASK-003 | Creare `Attributes/AgxiomeAgentAttribute.cs` — `[AttributeUsage(Class)] AgxiomeAgentAttribute(string id, string description)`. Metadata extraction senza istanziare la classe             |           |      |
| TASK-004 | Creare `Templates/AgentCodeTemplate.cs` — template C# con placeholder `{{AGENT_ID}}`, `{{DESCRIPTION}}`, `{{LOGIC_BODY}}`                                                                  |           |      |
| TASK-005 | Creare `Templates/AgentTemplateExamples.cs` — 3 esempi few-shot (trasformazione testo, validazione JSON, classificazione) per system prompt LLM                                            |           |      |

### Phase 2 — Hot-Folder Watcher e Catalogo Lazy

- GOAL-002: L'utente copia un `.cs` nella cartella dedicata → appare nel catalogo istantaneamente, senza compilazione

```
~/.agxioma/
  agents/
    {userId}/
      src/          ← utente copia qui i suoi file .cs
        sentiment-agent.cs
        invoice-parser.cs
      bin/          ← cache assembly compilati (auto-generata)
        sentiment-agent.dll
        sentiment-agent.sha256
```

| Task     | Description                                                                                                                                                                                                                    | Completed | Date |
| -------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | --------- | ---- |
| TASK-006 | Creare `HotFolder/AgentDropFolder.cs` — `FileSystemWatcher` su `src/*.cs`. Debounce 300ms. Emette: `AgentFileDiscovered`, `AgentFileModified`, `AgentFileRemoved`                                                              |           |      |
| TASK-007 | Creare `HotFolder/AgentFileScanner.cs` — legge `.cs` senza compilare, estrae `[AgxiomeAgent("id","desc")]` via Roslyn `SyntaxTree.ParseText()` (parse-only). Ritorna `AgentFileMetadata(AgentId, Description, FilePath, SourceHash)` |           |      |
| TASK-008 | Creare `Registry/DiscoveredAgentEntry.cs` — record: `(AgentId, Description, FilePath, SourceHash, AgentEntryStatus, DiscoveredAt)`. Status: `Discovered / Compiling / Compiled / Failed / NeedsRecompile`                     |           |      |
| TASK-009 | Creare `Registry/DynamicAgentRegistry.cs` — `ConcurrentDictionary<string, DiscoveredAgentEntry>`. Metodi: `Register`, `GetAll(userId)`, `GetStatus(agentId)`, `MarkCompiled`, `Remove`                                       |           |      |
| TASK-010 | Creare `HotFolder/AgentDropFolderService.cs` — `IHostedService` che avvia `AgentDropFolder` per ogni utente, riceve eventi, chiama `AgentFileScanner`, aggiorna `DynamicAgentRegistry`. Hot-reload su file modificato         |           |      |

### Phase 3 — Roslyn Compiler Engine (lazy, sandboxed, cached)

- GOAL-003: Compilare sorgente C# in assembly isolato solo al primo utilizzo, con cache SHA256 per evitare ricompilazioni al riavvio

| Task     | Description                                                                                                                                                                                                                          | Completed | Date |
| -------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | --------- | ---- |
| TASK-011 | Creare `Compilation/AgentCodeValidator.cs` — visita `SyntaxTree`, blocca namespace vietati su `UsingDirectiveSyntax` e `MemberAccessExpressionSyntax`. Ritorna `ValidationResult(bool Ok, string[] Violations)`                     |           |      |
| TASK-012 | Creare `Compilation/AgentCompiler.cs` — `CompileAsync(string source, string agentId)`. `CSharpCompilation.Create()` con allowlist `MetadataReference`, `Emit()` su `MemoryStream`, scrive `.dll` + `.sha256` in `bin/`             |           |      |
| TASK-013 | Creare `Isolation/AgentAssemblyContext.cs` — `AssemblyLoadContext` con `IsCollectible=true`, `LoadFromStream()`, `Unload()`. `WeakReference<AgentAssemblyContext>` per verifica GC corretto                                         |           |      |
| TASK-014 | Creare `Bridge/DynamicAgentAdapter.cs` — proxy `ISimpleDynamicAgent` → `IAgxiomeAgent`. Delega `RunAsync(AgentRequest, ct)` a `ISimpleDynamicAgent.RunAsync(request.Prompt, ct)`                                                    |           |      |
| TASK-015 | Creare `Compilation/LazyAgentCompiler.cs` — `ConcurrentDictionary<string, Lazy<Task<CompiledAgent>>>`. Prima chiamata → compila e cacha. Chiamate concorrenti attendono la stessa `Lazy<Task>` (thread-safe, zero race condition)    |           |      |
| TASK-016 | Creare `Persistence/AgentCacheStore.cs` — al boot: scansiona `bin/`, confronta SHA256 di `.dll` e `.cs`. Match → `LoadFromFile()` diretto. Mismatch → status `NeedsRecompile`. Zero ricompilazione se codice invariato             |           |      |

### Phase 4 — LLM Code Generation Pipeline

- GOAL-004: Orchestratore descrive in linguaggio naturale → agente compilato, registrato e pronto in < 3 secondi

| Task     | Description                                                                                                                                                                                                                               | Completed | Date |
| -------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------- | ---- |
| TASK-017 | Creare `Generation/AgentGenerationRequest.cs` — record: `(string UserId, string Description, string? ExampleInput, string? ExampleOutput, string[]? AllowedNamespaces)`                                                                  |           |      |
| TASK-018 | Creare `Generation/AgentCodeGenerator.cs` — pipeline: system prompt → LLM → estrai blocco C# (regex ` ```csharp...``` `) → `AgentCodeValidator` → `AgentCompiler` → `DynamicAgentRegistry.Register()` → salva `.cs` in `src/` utente  |           |      |
| TASK-019 | Creare `Generation/Prompts/AgentGenerationSystemPrompt.cs` — system prompt con: interfaccia `ISimpleDynamicAgent`, attributo `[AgxiomeAgent]`, template, 3 esempi few-shot, allowlist/blocklist namespace, regola "solo ```csharp```"    |           |      |
| TASK-020 | Creare `Generation/AgentGenerationResult.cs` — record: `(string AgentId, string GeneratedCode, bool Success, string[] Errors, TimeSpan TotalTime, string? SavedFilePath)`                                                               |           |      |
| TASK-021 | Implementare retry loop in `AgentCodeGenerator`: compilazione fallisce → errori Roslyn inviati all'LLM come feedback ("fix these compiler errors: ...") — max 3 iterazioni                                                               |           |      |

### Phase 5 — Skills.sh Integration

- GOAL-005: Importare skills dal marketplace senza compilare né riavviare la soluzione

| Task     | Description                                                                                                                                                                       | Completed | Date |
| -------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------- | ---- |
| TASK-022 | Creare `Skills/SkillsShClient.cs` — HTTP client: `SearchAsync(q)`, `GetSkillAsync(id)`, `ListCategoriesAsync()`. Cache in memoria 1h, rispetta `Retry-After`                     |           |      |
| TASK-023 | Creare `Skills/SkillsShModels.cs` — record: `SkillDefinition(Id, Name, Description, SkillType, Endpoint?, SourceCode?, Parameters?)`                                             |           |      |
| TASK-024 | Creare `Skills/SkillAdapter.cs` — `HttpApi` → `IAgxiomeTool` HTTP wrapper. `CSharpFunction` → `AgentCompiler` su SourceCode embedded. `Prompt` → agente LLM puro                |           |      |
| TASK-025 | Creare `Skills/SkillsShRegistry.cs` — `ImportAsync(skillId, userId)` persiste in `~/.agxioma/skills/{userId}.json`. Status: `Trusted / Pending / Blocked`                       |           |      |

### Phase 6 — REST API e DI

- GOAL-006: Esporre tutto via endpoint HTTP con un singolo `services.AddDynamicAgentRuntime()`

| Task     | Description                                                                                                                                                                                                                          | Completed | Date |
| -------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | --------- | ---- |
| TASK-026 | Creare `AGXIOME.Runtime/Controllers/DynamicAgentController.cs` — `GET /agents/user/{userId}` (catalogo con status inclusi Discovered), `POST /agents/generate`, `POST /agents/{agentId}/compile` (forza lazy compile), `DELETE /agents/{agentId}` |           |      |
| TASK-027 | Creare `AGXIOME.Runtime/Controllers/SkillsController.cs` — `GET /skills/search?q=`, `POST /skills/{skillId}/import/{userId}`, `GET /skills/user/{userId}`, `PATCH /skills/{skillId}/trust`                                          |           |      |
| TASK-028 | Aggiornare `AgxiomeServiceCollectionExtensions.cs` — `services.AddDynamicAgentRuntime(opts)`: registra tutti i servizi + `AgentDropFolderService` come `IHostedService`                                                             |           |      |

### Phase 7 — Orchestrator Tools (sistema auto-espandibile)

- GOAL-007: L'orchestratore può creare e trovare strumenti durante l'esecuzione di un piano, senza intervento umano

| Task     | Description                                                                                                                                                                                                           | Completed | Date |
| -------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------- | ---- |
| TASK-029 | Creare `AGXIOME.Core/Tools/GenerateAgentTool.cs` — schema: `{ description, example_input?, example_output? }`. Chiama `AgentCodeGenerator`. Ritorna `{ agent_id, status: "ready", file_path }`                      |           |      |
| TASK-030 | Creare `AGXIOME.Core/Tools/ImportSkillTool.cs` — schema: `{ skill_id, user_id }`. Chiama `SkillsShClient` + `SkillAdapter`. Ritorna skill_id + tipo adattamento applicato                                            |           |      |
| TASK-031 | Creare `AGXIOME.Core/Tools/ListAvailableAgentsTool.cs` — zero-param: ritorna JSON con tutti gli agenti nel registry (id, description, status). Impedisce all'orchestratore di ricreare agenti già disponibili        |           |      |

## 3. Alternatives

- **ALT-001**: Usare `dotnet script` (CSX scripting) invece di Roslyn puro — scartato perché richiede dotnet CLI esterno, più lento, meno controllo sulla sandbox
- **ALT-002**: Usare IronPython/Jint per linguaggi scriptabili invece di C# — scartato perché perderemmo la type-safety e la compatibilità nativa con le interfacce AGXIOMA
- **ALT-003**: Plugin system file-based con `Directory.GetFiles("*.dll")` e reflection — scartato perché non permette generazione LLM on-the-fly, richiede deploy manuale
- **ALT-004**: Usare WebAssembly (Wasmtime) come sandbox — interessante per v2, ma overhead eccessivo per casi d'uso comuni
- **ALT-005**: Per Skills.sh, generare codice Python e wrapparlo — scartato perché introduce dipendenza Python e rompe il modello .NET-only

---

## 4. Dependencies

- **DEP-001**: `Microsoft.CodeAnalysis.CSharp` >= 4.x (Roslyn) — compiler as a service
- **DEP-002**: `System.Runtime.Loader` (built-in .NET 10) — `AssemblyLoadContext`
- **DEP-003**: `AGXIOME.LLMWIKY` — per la generazione codice via LLM
- **DEP-004**: `AGXIOME.Shared` — `IAgxiomeAgent`, `IAgxiomeTool`, `AgentRequest/Response`
- **DEP-005**: Skills.sh HTTP API — endpoint da verificare (`https://api.skills.sh/v1/` o simile)
- **DEP-006**: `System.Text.Json` (built-in) — serializzazione registry persistenza

---

## 5. Files

- **FILE-001**: `AGXIOME.DynamicRuntime/AGXIOME.DynamicRuntime.csproj` — nuovo progetto
- **FILE-002**: `AGXIOME.DynamicRuntime/Interfaces/ISimpleDynamicAgent.cs` — contratto minimo agenti dinamici
- **FILE-003**: `AGXIOME.DynamicRuntime/Attributes/AgxiomeAgentAttribute.cs` — attributo per metadata extraction
- **FILE-004**: `AGXIOME.DynamicRuntime/Templates/AgentCodeTemplate.cs` — template C# con placeholder
- **FILE-005**: `AGXIOME.DynamicRuntime/Templates/AgentTemplateExamples.cs` — esempi few-shot
- **FILE-006**: `AGXIOME.DynamicRuntime/HotFolder/AgentDropFolder.cs` — FileSystemWatcher debounced
- **FILE-007**: `AGXIOME.DynamicRuntime/HotFolder/AgentFileScanner.cs` — estrazione metadati senza compilazione
- **FILE-008**: `AGXIOME.DynamicRuntime/HotFolder/AgentDropFolderService.cs` — IHostedService watcher
- **FILE-009**: `AGXIOME.DynamicRuntime/Registry/DiscoveredAgentEntry.cs` — entry con status lazy
- **FILE-010**: `AGXIOME.DynamicRuntime/Registry/DynamicAgentRegistry.cs` — catalogo thread-safe
- **FILE-011**: `AGXIOME.DynamicRuntime/Compilation/AgentCodeValidator.cs` — analisi statica sandbox
- **FILE-012**: `AGXIOME.DynamicRuntime/Compilation/AgentCompiler.cs` — Roslyn CSharpCompilation
- **FILE-013**: `AGXIOME.DynamicRuntime/Compilation/LazyAgentCompiler.cs` — lazy thread-safe compile
- **FILE-014**: `AGXIOME.DynamicRuntime/Isolation/AgentAssemblyContext.cs` — AssemblyLoadContext isolato
- **FILE-015**: `AGXIOME.DynamicRuntime/Bridge/DynamicAgentAdapter.cs` — ISimpleDynamicAgent → IAgxiomeAgent
- **FILE-016**: `AGXIOME.DynamicRuntime/Persistence/AgentCacheStore.cs` — SHA256 cache boot-time
- **FILE-017**: `AGXIOME.DynamicRuntime/Generation/AgentCodeGenerator.cs` — LLM → C# → compile
- **FILE-018**: `AGXIOME.DynamicRuntime/Generation/Prompts/AgentGenerationSystemPrompt.cs` — system prompt
- **FILE-019**: `AGXIOME.DynamicRuntime/Skills/SkillsShClient.cs` — HTTP client Skills.sh
- **FILE-020**: `AGXIOME.DynamicRuntime/Skills/SkillAdapter.cs` — SkillDefinition → IAgxiomeTool
- **FILE-021**: `AGXIOME.DynamicRuntime/Skills/SkillsShRegistry.cs` — catalogo skills per utente
- **FILE-022**: `AGXIOME.Runtime/Controllers/DynamicAgentController.cs` — REST API agenti
- **FILE-023**: `AGXIOME.Runtime/Controllers/SkillsController.cs` — REST API skills
- **FILE-024**: `AGXIOME.Core/Tools/GenerateAgentTool.cs` — tool orchestratore: genera agente
- **FILE-025**: `AGXIOME.Core/Tools/ImportSkillTool.cs` — tool orchestratore: importa skill
- **FILE-026**: `AGXIOME.Core/Tools/ListAvailableAgentsTool.cs` — tool orchestratore: lista catalogo

---

## 6. Testing

- **TEST-001**: `AgentFileScanner_ExtractMetadata_WithoutCompiling` — dato un `.cs` con `[AgxiomeAgent]`, estrae id+description senza compilare
- **TEST-002**: `AgentDropFolder_NewFile_ShouldAppearInRegistry` — copia file in `src/`, verifica `DynamicAgentRegistry.GetStatus() == Discovered`
- **TEST-003**: `AgentDropFolder_ModifiedFile_ShouldTriggerNeedsRecompile` — modifica file già `Compiled`, verifica status `NeedsRecompile`
- **TEST-004**: `LazyAgentCompiler_FirstCall_ShouldCompileAndCache` — prima chiamata su agente `Discovered` → status diventa `Compiled`
- **TEST-005**: `LazyAgentCompiler_ConcurrentCalls_ShouldCompileOnce` — 10 chiamate concorrenti sullo stesso `AgentId` → Roslyn invocato una sola volta
- **TEST-006**: `AgentCacheStore_Reload_ShouldSkipCompilationIfHashMatch` — riavvio simulato, `.dll` presente con SHA256 corretto → zero ricompilazione
- **TEST-007**: `AgentCodeValidator_ForbiddenNamespace_ShouldFail` — codice con `System.IO.File.ReadAllText` → `ValidationResult.Ok == false`
- **TEST-008**: `AgentAssemblyContext_Unload_ShouldFreeMemory` — carica, usa, unload; `WeakReference.IsAlive == false` dopo GC
- **TEST-009**: `AgentCodeGenerator_FromDescription_ShouldProduceValidCode` — integration test con mock LLM provider
- **TEST-010**: `AgentCodeGenerator_RetryOnCompileError_ShouldFix` — prima generazione fallisce, secondo tentativo con feedback Roslyn → success
- **TEST-011**: `SkillAdapter_HttpSkill_ShouldWrapAsIAgxiomeTool` — skill HTTP → tool invocabile
- **TEST-012**: End-to-end: `orchestratore → "crea agente sentiment" → GenerateAgentTool → genera → compila → agente disponibile in GroupChatSwarm`

---

## 7. Risks & Assumptions

- **RISK-001**: Codice generato dall'LLM potrebbe essere malformato (classi incomplete, sintassi errata) — mitigato da: retry automatico con errori Roslyn nel prompt di feedback + max 3 tentativi
- **RISK-002**: Memory leak se `AssemblyLoadContext` non viene unloaded correttamente — mitigato da: pattern `WeakReference` + GC.Collect() esplicito + test unitario dedicato (TEST-003)
- **RISK-003**: Skills.sh API cambia o non è disponibile — mitigato da: fallback a catalogo locale cached + interfaccia `ISkillsProvider` scambiabile
- **RISK-004**: L'LLM potrebbe generare codice che bypassi la sandbox (runtime reflection evasion) — mitigato da: analisi statica Roslyn pre-compilazione (TASK-013) + `AssemblyLoadContext` non ha accesso al FileSystem dell'host
- **RISK-005**: Overhead Roslyn al primo avvio (cold start ~300ms per JIT del compiler) — accettabile, poi warm
- **ASSUMPTION-001**: Skills.sh espone una API REST standard (JSON) — da verificare prima di TASK-014
- **ASSUMPTION-002**: Gli agenti generati hanno bisogno solo di `ILLMProvider` come dipendenza iniettata — casi più complessi (DB, HTTP) richiederebbero estensione della sandbox
- **ASSUMPTION-003**: La persistenza locale (`~/.agxioma/`) è accettabile per MVP; un DB (PostgreSQL via AGXIOME.Storage) può essere integrato in v2

---

## 8. Architecture Diagram

```
╔══════════════════════════════════════════════════════════════════╗
║              VETTORE 1 — HOT-FOLDER DROP                         ║
╚══════════════════════════════════════════════════════════════════╝

Utente copia sentiment-agent.cs → ~/.agxioma/agents/{userId}/src/
   │
   ▼ FileSystemWatcher (debounce 300ms)
AgentDropFolder emette AgentFileDiscovered
   │
   ▼ AgentFileScanner
Roslyn SyntaxTree.ParseText() → estrae [AgxiomeAgent("id","desc")]
NESSUNA compilazione — solo parsing sintattico
   │
   ▼ DynamicAgentRegistry
DiscoveredAgentEntry { Status = Discovered }    ← appare nel catalogo
   │                                              ← RAM: solo metadati
   ▼ (prima RunAsync() sull'agente)
LazyAgentCompiler — Lazy<Task<CompiledAgent>>
   │  AgentCodeValidator → blocca namespace vietati
   │  AgentCompiler → CSharpCompilation.Emit() → MemoryStream
   │  Scrive bin/sentiment-agent.dll + bin/sentiment-agent.sha256
   │
   ▼ AgentAssemblyContext (IsCollectible=true)
LoadFromStream() → ISimpleDynamicAgent instance
   │
   ▼ DynamicAgentAdapter
IAgxiomeAgent proxy — usabile in MoA, GroupChat, DAG, Hierarchical


╔══════════════════════════════════════════════════════════════════╗
║              VETTORE 2 — LLM CODE GENERATION                     ║
╚══════════════════════════════════════════════════════════════════╝

Orchestrator / Utente: "crea un agente che analizza sentiment"
   │ GenerateAgentTool (IAgxiomeTool, function-calling)
   ▼
AgentCodeGenerator
   │ system prompt + template + esempi few-shot
   │ LLM genera C# → estrai blocco ```csharp```
   │ AgentCodeValidator → se ok
   │ AgentCompiler → assembly compilato
   │ Salva .cs in src/  ← hot-folder lo rileva → appare nel catalogo
   ▼
DynamicAgentRegistry { Status = Compiled }     ← pronto in < 3s

     ↺ se compilazione fallisce:
     AgentCodeGenerator rimanda errori Roslyn all'LLM
     (max 3 tentativi auto-fix)


╔══════════════════════════════════════════════════════════════════╗
║              VETTORE 3 — SKILLS.SH IMPORT                        ║
╚══════════════════════════════════════════════════════════════════╝

Utente: "importa skill web-scraper da Skills.sh"
   │ ImportSkillTool
   ▼ SkillsShClient → GET api.skills.sh/v1/skills/{id}
   │ SkillDefinition { Type = HttpApi | CSharpFunction | Prompt }
   ▼ SkillAdapter
     HttpApi       → IAgxiomeTool (thin HTTP wrapper, no compile)
     CSharpFunction→ AgentCompiler su SourceCode embedded
     Prompt        → agente LLM puro (zero compile)
   ▼ SkillsShRegistry.ImportAsync(userId)
Disponibile in tutti i swarm/workflow dell'utente


╔══════════════════════════════════════════════════════════════════╗
║              BOOT-TIME CACHE (riavvio zero-downtime)             ║
╚══════════════════════════════════════════════════════════════════╝

AgentCacheStore.LoadAllAsync(userId)
  ├── Per ogni .cs in src/ → calcola SHA256
  ├── Se bin/{name}.dll esiste e .sha256 match
  │     → AssemblyLoadContext.LoadFromFile()   (zero Roslyn)
  │     → Status = Compiled
  └── Se mismatch o .dll mancante
        → Status = NeedsRecompile (lazy: compila al primo uso)
```

---

## 8. Related Specifications / Further Reading

- [Roslyn Scripting API docs](https://github.com/dotnet/roslyn/blob/main/docs/wiki/Scripting-API-Samples.md)
- [AssemblyLoadContext — collectible assemblies](https://learn.microsoft.com/en-us/dotnet/core/dependency-loading/collectible-assemblies)
- [Skills.sh](https://skills.sh) — marketplace skills AI
- [plan/upgrade-agxioma-competitive-1.md](plan/upgrade-agxioma-competitive-1.md) — piano strategico principale
