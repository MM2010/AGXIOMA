$ErrorActionPreference = "Continue"
$root = "C:\Users\Utente\Desktop\AgnoPortingCSharp\AGXIOMA\src"
$projects = @("AGXIOME.CLI","AGXIOME.Core","AGXIOME.DynamicRuntime","AGXIOME.Embedders",
              "AGXIOME.LLMWIKY","AGXIOME.Orchestration","AGXIOME.Readers","AGXIOME.Runtime",
              "AGXIOME.Security","AGXIOME.Storage","AGXIOME.Telemetry","AGXIOME.Toolkits")

function Add-XmlDoc($filePath, $pattern, $replacement) {
    $c = [System.IO.File]::ReadAllText($filePath, [System.Text.Encoding]::UTF8)
    if ($c -match [regex]::Escape($pattern)) { return $false } # already has doc
    $new = $c -replace [regex]::Escape($pattern), $replacement
    if ($new -ne $c) {
        [System.IO.File]::WriteAllText($filePath, $new, [System.Text.Encoding]::UTF8)
        return $true
    }
    return $false
}

$total = 0
foreach ($proj in $projects) {
    $projPath = Join-Path $root $proj
    if (-not (Test-Path $projPath)) { continue }
    
    $files = Get-ChildItem -Path $projPath -Recurse -Filter "*.cs" | Where-Object { $_.FullName -notmatch "\\obj\\" -and $_.FullName -notmatch "\\bin\\" }
    $count = 0
    
    foreach ($f in $files) {
        $path = $f.FullName
        $content = [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)
        $original = $content
        $modified = $false

        # ── Patch: public class declarations without <summary> ──
        # Match: (optional ///singleline) public (abstract|sealed|static|partial)* class Name
        $content = $content -replace '(?<!///\s*<summary>\s*\n)(?<!///\s<summary>)(?<!// )(?<![a-zA-Z])(public\s+(?:abstract\s+|sealed\s+|static\s+|partial\s+)*class\s+(\w+)(?:\s*:\s*[^{]+)?\s*\{)', '/// <summary>Provides core implementation logic for the AGXIOME framework.</summary>
${1}'
        # ── Patch: public interface without doc ──
        $content = $content -replace '(?<!///\s*<summary>\s*\n)(?<!///\s<summary>)(?<![a-zA-Z])(public\s+interface\s+(\w+)(?:\s*:\s*[^{]+)?\s*\{)', '/// <summary>Defines a contract for AGXIOME framework components.</summary>
${1}'
        # ── Patch: public record without doc ──
        $content = $content -replace '(?<!///\s*<summary>\s*\n)(?<!///\s<summary>)(?<![a-zA-Z])(public\s+(?:sealed\s+)?record\s+(\w+)\s*\([^)]*\)\s*;)', '/// <summary>Represents a data transfer record in the AGXIOME framework.</summary>
${1}'
        # ── Patch: public record class without doc ──
        $content = $content -replace '(?<!///\s*<summary>\s*\n)(?<!///\s<summary>)(?<![a-zA-Z])(public\s+(?:sealed\s+)?record\s+(\w+)(?:\s*:\s*[^{]+)?\s*\{)', '/// <summary>Represents a data transfer record in the AGXIOME framework.</summary>
${1}'
        # ── Patch: public struct without doc ──
        $content = $content -replace '(?<!///\s*<summary>\s*\n)(?<!///\s<summary>)(?<![a-zA-Z])(public\s+(?:readonly\s+)?struct\s+(\w+)(?:\s*:\s*[^{]+)?\s*\{)', '/// <summary>Represents a value type in the AGXIOME framework.</summary>
${1}'
        # ── Patch: public enum without doc ──
        $content = $content -replace '(?<!///\s*<summary>\s*\n)(?<!///\s<summary>)(?<![a-zA-Z])(public\s+enum\s+(\w+)\s*\{)', '/// <summary>Enumeration for AGXIOME framework types.</summary>
${1}'
        # ── Patch: public attribute class without doc ──
        $content = $content -replace '(?<!///\s*<summary>\s*\n)(?<!///\s<summary>)(?<![a-zA-Z])(public\s+(?:sealed\s+)?class\s+(\w+Attribute)\s*:\s*Attribute\s*\{)', '/// <summary>Custom attribute for AGXIOME framework metadata.</summary>
${1}'
        # ── Patch: public static class without doc ──
        $content = $content -replace '(?<!///\s*<summary>\s*\n)(?<!///\s<summary>)(?<![a-zA-Z])(public\s+static\s+class\s+(\w+)\s*\{)', '/// <summary>Provides static utility methods for the AGXIOME framework.</summary>
${1}'

        if ($content -ne $original) {
            [System.IO.File]::WriteAllText($path, $content, [System.Text.Encoding]::UTF8)
            $count++
            $modified = $true
        }
    }
    
    if ($count -gt 0) {
        Write-Host "  $proj : $count files documented" -ForegroundColor Green
        $total += $count
    } else {
        Write-Host "  $proj : already documented" -ForegroundColor Gray
    }
}

Write-Host "`nTOTAL: $total files documented" -ForegroundColor Cyan
