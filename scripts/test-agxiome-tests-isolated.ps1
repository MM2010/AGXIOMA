param(
    [string]$Filter,
    [switch]$SkipBuildServerShutdown
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$artifacts = Join-Path $env:TEMP "agxiome-tests-isolated-$timestamp"

New-Item -ItemType Directory -Path $artifacts -Force | Out-Null

Push-Location $repoRoot
try {
    if (-not $SkipBuildServerShutdown) {
        dotnet build-server shutdown
    }

    $command = @(
        'test'
        '.\src\AGXIOME.Tests\AGXIOME.Tests.csproj'
        '--artifacts-path'
        $artifacts
    )

    if (-not [string]::IsNullOrWhiteSpace($Filter)) {
        $command += '--filter'
        $command += $Filter
    }

    Write-Host "[AGXIOME] Running isolated tests under: $artifacts"
    Write-Host "[AGXIOME] Command: dotnet $($command -join ' ')"

    & dotnet @command
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}