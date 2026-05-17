param(
    [string[]]$Slices = @(
        'f4-sqlserver',
        'f4-localdb',
        'f4-postgres',
        'f4-postgres-compat',
        'f8-core',
        't68-openai-vision'
    ),
    [switch]$SkipBuildServerShutdown
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$testProject = '.\src\AGXIOME.Tests\AGXIOME.Tests.csproj'
$testAssembly = '.\src\AGXIOME.Tests\bin\Debug\net10.0\AGXIOME.Tests.dll'

Write-Host '[AGXIOME][PROOF] Manual wrapper only.'
Write-Host '[AGXIOME][PROOF] Official workflow remains the dedicated xUnit tests in src\AGXIOME.Tests.'
Write-Host '[AGXIOME][PROOF] This script exists to run isolated proof slices with the same compiled test assembly across F4, F8 and T-68.'

function Remove-ProofArtifacts {
    param([string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path) -or -not (Test-Path -LiteralPath $Path)) {
        return
    }

    Remove-Item -LiteralPath $Path -Recurse -Force -ErrorAction SilentlyContinue
}

function Clear-StaleProofArtifacts {
    $staleArtifacts = Get-ChildItem -Path $env:TEMP -Directory -Filter 'agxiome-proof-*' -ErrorAction SilentlyContinue
    foreach ($artifact in $staleArtifacts) {
        Remove-ProofArtifacts -Path $artifact.FullName
    }
}

$sliceSpecs = @{
    'f4-sqlserver' = [pscustomobject]@{
        Name = 'F4 SQL Server real proof'
        Filter = 'FullyQualifiedName~SqlServerCommandStoreTests'
        MinTotal = 1
        DockerImage = 'mcr.microsoft.com/mssql/server:2022-latest'
        RequiredFullNamePatterns = @(
            'AGXIOME\.Tests\.SqlServerCommandStoreTests\.SaveUpdateDelete_WithRealSqlServerConnection_WorksWhenConfigured$'
        )
        MinRequiredMatches = 1
    }
    'f4-localdb' = [pscustomobject]@{
        Name = 'F4 SQL Server LocalDB real proof'
        Filter = 'FullyQualifiedName~SqlServerLocalDbStoreTests'
        MinTotal = 1
        DockerImage = $null
        RequiredFullNamePatterns = @(
            'AGXIOME\.Tests\.SqlServerLocalDbStoreTests\.SaveUpdateDelete_WithRealLocalDbConnection_WorksWhenConfigured$'
        )
        MinRequiredMatches = 1
    }
    'f4-postgres' = [pscustomobject]@{
        Name = 'F4 PostgreSQL real proof'
        Filter = 'FullyQualifiedName~PostgresCommandStoreTests'
        MinTotal = 1
        DockerImage = 'postgres:16-alpine'
        RequiredFullNamePatterns = @(
            'AGXIOME\.Tests\.PostgresCommandStoreTests\.SaveUpdateDelete_WithRealPostgresConnection_WorksWhenConfigured$'
        )
        MinRequiredMatches = 1
    }
    'f4-postgres-compat' = [pscustomobject]@{
        Name = 'F4 PostgreSQL-compatible variants proof'
        Filter = 'FullyQualifiedName~NeonCommandStoreTests|FullyQualifiedName~SupabaseCommandStoreTests|FullyQualifiedName~CockroachDbCommandStoreTests|FullyQualifiedName~TimescaleDbCommandStoreTests'
        MinTotal = 4
        DockerImage = 'postgres:16-alpine'
        RequiredFullNamePatterns = @(
            'AGXIOME\.Tests\.NeonCommandStoreTests\.SaveUpdateDelete_WithPostgresCompatConnection_Works$'
            'AGXIOME\.Tests\.SupabaseCommandStoreTests\.SaveUpdateDelete_WithPostgresCompatConnection_Works$'
            'AGXIOME\.Tests\.CockroachDbCommandStoreTests\.SaveUpdateDelete_WithPostgresCompatConnection_Works$'
            'AGXIOME\.Tests\.TimescaleDbCommandStoreTests\.SaveUpdateDelete_WithPostgresCompatConnection_Works$'
        )
        MinRequiredMatches = 4
    }
    'f8-core' = [pscustomobject]@{
        Name = 'F8 tools infrastructure isolated proof'
        Filter = 'FullyQualifiedName~ToolResultCacheTests|FullyQualifiedName~ToolCallLimiterTests|FullyQualifiedName~StopAfterToolEvaluatorTests|FullyQualifiedName~ToolFilterTests|FullyQualifiedName~CallableFactoryTests|FullyQualifiedName~ToolMetricsCollectorTests|FullyQualifiedName~AgxiomeAgentToolsIntegrationTests'
        MinTotal = 7
        DockerImage = $null
        RequiredFullNamePatterns = @(
            'AGXIOME\.Tests\.ToolResultCacheTests\.SetThenGet_ReturnsCachedResult$'
            'AGXIOME\.Tests\.ToolCallLimiterTests\.TotalCallsExceeded_BlocksFurtherCalls$'
            'AGXIOME\.Tests\.StopAfterToolEvaluatorTests\.AfterAnyCall_StopsAlways$'
            'AGXIOME\.Tests\.ToolFilterTests\.Allowlist_OnlyListedToolsPass$'
            'AGXIOME\.Tests\.CallableFactoryTests\.CreateCallable_OpenAi_HasCorrectStructure$'
            'AGXIOME\.Tests\.ToolMetricsCollectorTests\.RecordSuccess_IncrementsSuccessCount$'
            'AGXIOME\.Tests\.AgxiomeAgentToolsIntegrationTests\.AutoStopRule_StopsAgentBeforeToolExecution$'
        )
        MinRequiredMatches = 7
    }
    't68-openai-vision' = [pscustomobject]@{
        Name = 'T-68 OpenAI Vision isolated proof'
        Filter = 'FullyQualifiedName~OpenAiVisionToolkitTests'
        MinTotal = 4
        DockerImage = $null
        RequiredFullNamePatterns = @(
            'AGXIOME\.Tests\.F12\.OpenAiVisionToolkitTests\.Execute_BothImageInputs_ReturnsError$'
            'AGXIOME\.Tests\.F12\.OpenAiVisionToolkitTests\.Execute_Ocr_UsesOcrPrompt_AndNormalizesText$'
            'AGXIOME\.Tests\.F12\.OpenAiVisionToolkitTests\.Execute_ObjectDetect_UsesDefaultPrompt_AndNormalizesObjects$'
            'AGXIOME\.Tests\.F12\.OpenAiVisionToolkitTests\.NormalizeVisionResult_Ocr_MapsTextPayload$'
        )
        MinRequiredMatches = 4
    }
}

function Get-TestSummary {
    param([string]$Output)

    $italian = [regex]::Match($Output, 'Riepilogo test:\s*totale:\s*(\d+);\s*non riuscito:\s*(\d+);\s*riuscito:\s*(\d+);\s*ignorato:\s*(\d+)')
    if ($italian.Success) {
        return [pscustomobject]@{
            Total = [int]$italian.Groups[1].Value
            Failed = [int]$italian.Groups[2].Value
            Passed = [int]$italian.Groups[3].Value
            Skipped = [int]$italian.Groups[4].Value
        }
    }

    $italianCompact = [regex]::Match($Output, 'Non superati:\s*(\d+)\.\s*Superati:\s*(\d+)\.\s*Ignorati:\s*(\d+)\.\s*Totale:\s*(\d+)')
    if ($italianCompact.Success) {
        return [pscustomobject]@{
            Total = [int]$italianCompact.Groups[4].Value
            Failed = [int]$italianCompact.Groups[1].Value
            Passed = [int]$italianCompact.Groups[2].Value
            Skipped = [int]$italianCompact.Groups[3].Value
        }
    }

    $english = [regex]::Match($Output, 'Total tests:\s*(\d+)\s*Passed:\s*(\d+)\s*Failed:\s*(\d+)\s*Skipped:\s*(\d+)')
    if ($english.Success) {
        return [pscustomobject]@{
            Total = [int]$english.Groups[1].Value
            Failed = [int]$english.Groups[3].Value
            Passed = [int]$english.Groups[2].Value
            Skipped = [int]$english.Groups[4].Value
        }
    }

    throw 'Unable to find a test summary in isolated runner output.'
}

function Get-TestSummaryFromTrx {
    param([string]$ArtifactsPath)

    $trxFile = Get-ChildItem -LiteralPath $ArtifactsPath -Recurse -Filter '*.trx' -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1

    if ($null -eq $trxFile) {
        throw "Unable to find a trx file under '$ArtifactsPath'."
    }

    [xml]$trx = Get-Content -LiteralPath $trxFile.FullName
    $counters = $trx.TestRun.ResultSummary.Counters
    if ($null -eq $counters) {
        throw "Unable to read test counters from '$($trxFile.FullName)'."
    }

    return [pscustomobject]@{
        Total = [int]$counters.total
        Failed = [int]$counters.failed
        Passed = [int]$counters.passed
        Skipped = [int]$counters.notExecuted
    }
}

function Get-TestResultsFromTrx {
    param([string]$ArtifactsPath)

    $trxFile = Get-ChildItem -LiteralPath $ArtifactsPath -Recurse -Filter '*.trx' -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1

    if ($null -eq $trxFile) {
        throw "Unable to find a trx file under '$ArtifactsPath'."
    }

    [xml]$trx = Get-Content -LiteralPath $trxFile.FullName
    $definitions = @{}

    foreach ($unitTest in @($trx.TestRun.TestDefinitions.UnitTest)) {
        if ($null -eq $unitTest) {
            continue
        }

        $fullName = $unitTest.name
        if ($unitTest.TestMethod) {
            $className = [string]$unitTest.TestMethod.className
            $methodName = [string]$unitTest.TestMethod.name
            if (-not [string]::IsNullOrWhiteSpace($className) -and -not [string]::IsNullOrWhiteSpace($methodName)) {
                $fullName = "$className.$methodName"
            }
        }

        $definitions[[string]$unitTest.id] = $fullName
    }

    $results = foreach ($unitTestResult in @($trx.TestRun.Results.UnitTestResult)) {
        if ($null -eq $unitTestResult) {
            continue
        }

        $resolvedName = $definitions[[string]$unitTestResult.testId]
        if ([string]::IsNullOrWhiteSpace($resolvedName)) {
            $resolvedName = [string]$unitTestResult.testName
        }

        [pscustomobject]@{
            FullName = $resolvedName
            Outcome = [string]$unitTestResult.outcome
            TestName = [string]$unitTestResult.testName
        }
    }

    return [pscustomobject]@{
        Summary = Get-TestSummaryFromTrx -ArtifactsPath $ArtifactsPath
        Results = @($results)
    }
}

function Invoke-IsolatedProofSlice {
    param(
        [int]$Index,
        [int]$TotalSlices,
        [string]$SliceName,
        [pscustomobject]$Spec
    )

    $timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $artifacts = Join-Path $env:TEMP "agxiome-proof-$SliceName-$timestamp"
    New-Item -ItemType Directory -Path $artifacts -Force | Out-Null
    $sliceStart = Get-Date

    try {
        $trxName = "$SliceName.trx"
        $command = @(
            'vstest'
            $testAssembly
            "/TestCaseFilter:$($Spec.Filter)"
            "/Logger:trx;LogFileName=$trxName"
            "/ResultsDirectory:$artifacts"
        )

        Write-Host "[AGXIOME][PROOF] Slice [$Index/$TotalSlices]: $($Spec.Name)"
        Write-Host "[AGXIOME][PROOF] Command: dotnet $($command -join ' ')"

        $start = (Get-Date).ToUniversalTime().ToString('o')
        $outputLines = @()
        & dotnet @command 2>&1 | Tee-Object -Variable outputLines | Out-Host
        $exitCode = $LASTEXITCODE
        $end = (Get-Date).ToUniversalTime().ToString('o')

        $output = ($outputLines | ForEach-Object { $_.ToString() }) -join [Environment]::NewLine

        $dockerEvents = ''
        if (-not [string]::IsNullOrWhiteSpace($Spec.DockerImage)) {
            $dockerEventLines = & docker events --since $start --until $end --filter "image=$($Spec.DockerImage)" --format '{{.Time}} {{.Type}} {{.Action}} {{json .Actor.Attributes}}' 2>&1
            $dockerEvents = ($dockerEventLines | ForEach-Object { $_.ToString() }) -join [Environment]::NewLine
            Write-Host '[AGXIOME][PROOF] Docker events:'
            Write-Host $dockerEvents
        }

        if ($exitCode -ne 0) {
            throw "Slice '$SliceName' failed with exit code $exitCode."
        }

        $trxResult = Get-TestResultsFromTrx -ArtifactsPath $artifacts
        $summary = $trxResult.Summary
        if ($summary.Total -lt $Spec.MinTotal) {
            throw "Slice '$SliceName' executed $($summary.Total) tests, expected at least $($Spec.MinTotal)."
        }

        $requiredMatches = @()
        foreach ($pattern in @($Spec.RequiredFullNamePatterns)) {
            $matched = @($trxResult.Results | Where-Object { $_.FullName -match $pattern -and $_.Outcome -ne 'NotExecuted' })
            $requiredMatches += $matched
        }

        $uniqueRequiredMatches = @($requiredMatches | Group-Object FullName | ForEach-Object { $_.Group[0] })
        if ($uniqueRequiredMatches.Count -lt $Spec.MinRequiredMatches) {
            $matchedNames = @($uniqueRequiredMatches | Select-Object -ExpandProperty FullName)
            throw "Slice '$SliceName' matched $($uniqueRequiredMatches.Count) required proof tests, expected at least $($Spec.MinRequiredMatches). Matched: $($matchedNames -join ', ')"
        }

        Write-Host '[AGXIOME][PROOF] Required proof tests executed:'
        $uniqueRequiredMatches | Select-Object FullName, Outcome | Format-Table -AutoSize | Out-Host

        if (-not [string]::IsNullOrWhiteSpace($Spec.DockerImage)) {
            if ($dockerEvents -notmatch 'container create') {
                throw "Slice '$SliceName' did not create a Docker container for image '$($Spec.DockerImage)'."
            }

            if ($dockerEvents -notmatch 'container start') {
                throw "Slice '$SliceName' did not start a Docker container for image '$($Spec.DockerImage)'."
            }

            if ($dockerEvents -notmatch 'container (destroy|die|kill)') {
                throw "Slice '$SliceName' did not show container teardown for image '$($Spec.DockerImage)'."
            }
        }

        return [pscustomobject]@{
            Slice = $SliceName
            Name = $Spec.Name
            Total = $summary.Total
            Passed = $summary.Passed
            Failed = $summary.Failed
            Skipped = $summary.Skipped
            DurationSeconds = [math]::Round(((Get-Date) - $sliceStart).TotalSeconds, 1)
            UsedDocker = -not [string]::IsNullOrWhiteSpace($Spec.DockerImage)
        }
    }
    finally {
        Remove-ProofArtifacts -Path $artifacts
    }
}

Push-Location $repoRoot
try {
    $overallStart = Get-Date

    if (-not $SkipBuildServerShutdown) {
        dotnet build-server shutdown | Out-Host
    }

    Clear-StaleProofArtifacts

    Write-Host '[AGXIOME][PROOF] Note: this script is reserved for real-provider F4 proof slices only.'
    Write-Host '[AGXIOME][PROOF] F4 docker-backed slices can take several minutes even when healthy.'
    Write-Host '[AGXIOME][PROOF] F8 and T-68 run through the same isolated compiled assembly to remove runner drift.'

    Write-Host '[AGXIOME][PROOF] Restoring test project once before per-slice isolated runs...'
    & dotnet restore $testProject | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "Restore failed with exit code $LASTEXITCODE."
    }

    Write-Host '[AGXIOME][PROOF] Building test assembly once with project references frozen...'
    & dotnet build $testProject '/p:BuildProjectReferences=false' | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed with exit code $LASTEXITCODE."
    }

    if (-not (Test-Path -LiteralPath $testAssembly)) {
        throw "Compiled test assembly '$testAssembly' was not found after build."
    }

    $dockerSlices = $Slices | Where-Object {
        $sliceSpecs.ContainsKey($_) -and -not [string]::IsNullOrWhiteSpace($sliceSpecs[$_].DockerImage)
    }

    if ($dockerSlices.Count -gt 0) {
        docker version | Out-Null
    }

    $results = foreach ($sliceName in $Slices) {
        if (-not $sliceSpecs.ContainsKey($sliceName)) {
            throw "Unknown slice '$sliceName'. Known slices: $($sliceSpecs.Keys -join ', ')"
        }

        $sliceIndex = [array]::IndexOf($Slices, $sliceName) + 1
        Invoke-IsolatedProofSlice -Index $sliceIndex -TotalSlices $Slices.Count -SliceName $sliceName -Spec $sliceSpecs[$sliceName]
    }

    Write-Host ''
    Write-Host '[AGXIOME][PROOF] Summary'
    $results | Format-Table Slice, Total, Passed, Failed, Skipped, DurationSeconds, UsedDocker -AutoSize | Out-Host
    Write-Host "[AGXIOME][PROOF] Total duration: $([math]::Round(((Get-Date) - $overallStart).TotalSeconds, 1))s"
}
finally {
    Pop-Location
}
