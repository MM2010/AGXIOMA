param(
    [switch]$OnlyGitleaks,
    [switch]$OnlyTrivy,
    [switch]$OnlySemgrep,
    [switch]$OnlyGrype
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path | Split-Path -Parent
$reportDir = Join-Path $repoRoot 'security-reports'
if (-not (Test-Path $reportDir)) { New-Item -ItemType Directory -Path $reportDir -Force | Out-Null }

$ns = 'agxiome-scan'
$script:exitCode = 0

function Run-Scan($name, $containerName, [string[]]$dockerArgs, $outFile) {
    $log = Join-Path $reportDir $outFile
    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host "  SCAN: $name" -ForegroundColor Cyan
    Write-Host "  Container: $containerName" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan

    $elapsed = [Diagnostics.Stopwatch]::StartNew()

    docker rm $containerName 2>$null | Out-Null

    $fullArgs = @('run', '--name', $containerName) + $dockerArgs
    & docker $fullArgs 2>&1 | Tee-Object -FilePath $log
    $ok = $LASTEXITCODE -eq 0

    $elapsed.Stop()
    $secs = $elapsed.Elapsed.TotalSeconds.ToString('F1')

    if ($ok) {
        Write-Host "  [$name] PASSED (${secs}s) - log: $log" -ForegroundColor Green
    } else {
        Write-Host "  [$name] FAILED (${secs}s) - log: $log" -ForegroundColor Red
        $script:exitCode = 1
    }

    docker rm $containerName 2>$null | Out-Null
}

if (-not $OnlyTrivy -and -not $OnlySemgrep -and -not $OnlyGrype) {
    $cn = $ns + '-gitleaks'
    $args = @('--rm', '-v', "${repoRoot}:/src:ro", 'zricethezav/gitleaks:latest', 'detect', '--source=/src', '--no-git', '--report-path=/src/security-reports/gitleaks-report.json')
    Run-Scan 'Gitleaks (secrets)' $cn $args 'gitleaks.log'
}

if (-not $OnlyGitleaks -and -not $OnlySemgrep -and -not $OnlyGrype) {
    $cn = $ns + '-trivy'
    $args = @('--rm', '-v', "${repoRoot}:/apps:ro", 'aquasec/trivy:latest', 'fs', '--format', 'table', '--cache-dir', '/tmp/trivy-cache', '/apps')
    Run-Scan 'Trivy (CVE)' $cn $args 'trivy.log'
}

if (-not $OnlyGitleaks -and -not $OnlyTrivy -and -not $OnlyGrype) {
    $cn = $ns + '-semgrep'
    $args = @('--rm', '-v', "${repoRoot}:/src:ro", 'returntocorp/semgrep:latest', 'semgrep', '--config=auto', '--output=/src/security-reports/semgrep-report.txt', '/src')
    Run-Scan 'Semgrep (SAST)' $cn $args 'semgrep.log'
}

if (-not $OnlyGitleaks -and -not $OnlyTrivy -and -not $OnlySemgrep) {
    $cn = $ns + '-grype'
    $args = @('--rm', '-v', "${repoRoot}:/apps:ro", 'anchore/grype:latest', 'dir:/apps', '--output', 'table')
    Run-Scan 'Grype (SCA)' $cn $args 'grype.log'
}

Write-Host "`n========================================" -ForegroundColor Cyan
if ($script:exitCode -eq 0) {
    Write-Host "  ALL SCANS PASSED" -ForegroundColor Green
} else {
    Write-Host "  SOME SCANS FAILED - check logs in $reportDir" -ForegroundColor Yellow
}
Write-Host "========================================" -ForegroundColor Cyan

$logCount = (Get-ChildItem $reportDir -File).Count
Write-Host "Reports generated: $logCount (in $reportDir)" -ForegroundColor Cyan

exit $script:exitCode
