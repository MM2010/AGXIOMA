$ErrorActionPreference = "Continue"
$root = "C:\Users\Utente\Desktop\AgnoPortingCSharp\AGXIOMA\src"
$projects = @("AGXIOME.CLI","AGXIOME.Core","AGXIOME.DynamicRuntime",
              "AGXIOME.LLMWIKY","AGXIOME.Orchestration","AGXIOME.Runtime",
              "AGXIOME.Security","AGXIOME.Storage","AGXIOME.Telemetry","AGXIOME.Toolkits")

$total = 0
foreach ($proj in $projects) {
    $projPath = Join-Path $root $proj
    if (-not (Test-Path $projPath)) { Write-Host "  SKIP: $proj"; continue }
    $files = Get-ChildItem $projPath -Recurse -Filter "*.cs" | Where-Object { $_.FullName -notmatch "\\obj\\" -and $_.FullName -notmatch "\\bin\\" }
    $count = 0
    foreach ($f in $files) {
        $path = $f.FullName
        $content = [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)
        if ($content -match '"""') { continue }
        if ($content -match '///\s*<summary>') { continue }
        $original = $content
        if ($content -match 'public\s+(?:(?:abstract|sealed|static|partial|readonly)\s+)*(class|interface|record|enum|struct)\s+(\w+)') {
            $kind = $matches[1]
            $matchPos = $content.IndexOf($matches[0])
            $before = $content.Substring(0, $matchPos)
            $lines = $before -split "`r?`n"
            $last2 = ($lines[-2..-1] | Where-Object { $_ -ne "" }) -join " "
            if ($last2 -match '///') { continue }
            $summary = switch ($kind) {
                "class"     { "/// <summary>Core implementation for the AGXIOME agentic orchestration framework.</summary>" }
                "interface" { "/// <summary>Contract definition for AGXIOME framework components.</summary>" }
                "record"    { "/// <summary>Data transfer record for the AGXIOME framework.</summary>" }
                "enum"      { "/// <summary>Enumeration type for the AGXIOME framework.</summary>" }
                "struct"    { "/// <summary>Value type for the AGXIOME framework.</summary>" }
            }
            $before2 = $content.Substring(0, $matchPos)
            $after2  = $content.Substring($matchPos)
            $content = $before2 + $summary + "`r`n" + $after2
        }
        if ($content -ne $original) {
            [System.IO.File]::WriteAllText($path, $content, [System.Text.Encoding]::UTF8)
            $count++
        }
    }
    Write-Host "  $proj : $count files"
    $total += $count
}
# Second pass: document ProviderEnums.cs explicitly
$enumFile = Join-Path $root "AGXIOME.Shared\Models\ProviderEnums.cs"
if (Test-Path $enumFile) {
    $c = [System.IO.File]::ReadAllText($enumFile, [System.Text.Encoding]::UTF8)
    $orig = $c
$c = $c -replace 'public enum LLMProviderType', '/// <summary>Supported LLM provider types (OpenAI, Anthropic, Ollama, local, etc.).</summary>' + "`r`n" + 'public enum LLMProviderType'
$c = $c -replace 'public enum StorageProviderType', '/// <summary>Supported storage/database provider types (PostgreSQL, SQLite, Redis, etc.).</summary>' + "`r`n" + 'public enum StorageProviderType'
$c = $c -replace 'public enum VectorDbProviderType', '/// <summary>Supported vector database provider types (Pinecone, Qdrant, Milvus, etc.).</summary>' + "`r`n" + 'public enum VectorDbProviderType'
$c = $c -replace 'public enum EmbedderType', '/// <summary>Supported embedding model provider types (OpenAI, Cohere, ONNX, etc.).</summary>' + "`r`n" + 'public enum EmbedderType'
$c = $c -replace 'public enum DocumentReaderType', '/// <summary>Supported document reader types (PDF, DOCX, Markdown, HTML, etc.).</summary>' + "`r`n" + 'public enum DocumentReaderType'
$c = $c -replace 'public enum ChunkerType', '/// <summary>Supported text chunking strategy types (Recursive, Semantic, SlidingWindow, etc.).</summary>' + "`r`n" + 'public enum ChunkerType'
$c = $c -replace 'public enum RerankerType', '/// <summary>Supported reranker model types (Cohere, Jina, Voyage, BGE, CrossEncoder).</summary>' + "`r`n" + 'public enum RerankerType'
$c = $c -replace 'public enum ToolkitCategory', '/// <summary>Categories for organizing prebuilt toolkits (Communication, Productivity, Dev, etc.).</summary>' + "`r`n" + 'public enum ToolkitCategory'
$c = $c -replace 'public enum AuthSchemeType', '/// <summary>Supported authentication scheme types (JWT, API Key, OAuth2, etc.).</summary>' + "`r`n" + 'public enum AuthSchemeType'
$c = $c -replace 'public enum RbacRole', '/// <summary>Role-Based Access Control roles (Admin, Operator, Developer, Viewer).</summary>' + "`r`n" + 'public enum RbacRole'
$c = $c -replace 'public enum RbacAction', '/// <summary>RBAC actions for permission checks (Read, Write, Delete, Execute, Admin).</summary>' + "`r`n" + 'public enum RbacAction'
$c = $c -replace 'public enum RbacResource', '/// <summary>RBAC resource types for access control (Agent, Team, Workflow, etc.).</summary>' + "`r`n" + 'public enum RbacResource'
$c = $c -replace 'public enum BackgroundJobType', '/// <summary>Types of background jobs (Cleanup, Rotation, HealthCheck, etc.).</summary>' + "`r`n" + 'public enum BackgroundJobType'
$c = $c -replace 'public enum BackgroundJobStatus', '/// <summary>Background job execution status (Pending, Running, Completed, Failed).</summary>' + "`r`n" + 'public enum BackgroundJobStatus'
$c = $c -replace 'public enum ResumeStrategy', '/// <summary>Strategies for resuming agent runs (FromCheckpoint, Replay, Restart).</summary>' + "`r`n" + 'public enum ResumeStrategy'
$c = $c -replace 'public enum CacheStrategy', '/// <summary>Cache eviction and refresh strategies (LRU, TTL, StaleWhileRevalidate).</summary>' + "`r`n" + 'public enum CacheStrategy'
$c = $c -replace 'public enum TokenizerAlgorithm', '/// <summary>Tokenizer algorithms for token counting (TikToken, WordPiece, BPE).</summary>' + "`r`n" + 'public enum TokenizerAlgorithm'
$c = $c -replace 'public enum CliCommand', '/// <summary>CLI command types for the AGXIOME command-line interface.</summary>' + "`r`n" + 'public enum CliCommand'
$c = $c -replace 'public enum CliOutputFormat', '/// <summary>CLI output format options (Json, Table, Plain, Yaml).</summary>' + "`r`n" + 'public enum CliOutputFormat'
$c = $c -replace 'public enum ModelCompatibilityStatus', '/// <summary>Model compatibility status (Compatible, Deprecated, RequiresMigration).</summary>' + "`r`n" + 'public enum ModelCompatibilityStatus'
$c = $c -replace 'public enum BridgeFramework', '/// <summary>External framework bridge types (LangChain, SemanticKernel, Claude).</summary>' + "`r`n" + 'public enum BridgeFramework'
    if ($c -ne $orig) {
        [System.IO.File]::WriteAllText($enumFile, $c, [System.Text.Encoding]::UTF8)
        Write-Host "  AGXIOME.Shared: ProviderEnums.cs (+21 enums)" -ForegroundColor Green
        $total += 1
    }
}
