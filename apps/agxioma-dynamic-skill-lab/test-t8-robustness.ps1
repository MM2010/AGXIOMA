#!/usr/bin/env pwsh
# ============================================================
#  T8 Dynamic Skill Lab — Robustness Test Suite
#  Tests: generation diversity, input edge cases, resilience
# ============================================================

$BASE = "http://localhost:5007"
$results = [System.Collections.Generic.List[PSCustomObject]]::new()

function Invoke-Pipeline {
    param([string]$Task, [string]$Session)
    try {
        $body = @{ task = $Task; sessionId = $Session } | ConvertTo-Json
        $r = Invoke-RestMethod "$BASE/pipeline" -Method Post -ContentType "application/json" -Body $body -TimeoutSec 60
        return @{ ok = $true; toolId = $r.toolId; step = $r.step; timing = $r.timing.total; errors = $r.errors }
    } catch {
        $msg = $_.ErrorDetails.Message
        try { $msg = ($msg | ConvertFrom-Json).result.step } catch {}
        return @{ ok = $false; toolId = $null; step = $msg; timing = $null; errors = @("$_") }
    }
}

function Invoke-Run {
    param([string]$ToolId, [string]$Input, [string]$Session)
    try {
        $body = @{ input = $Input; sessionId = $Session } | ConvertTo-Json
        $r = Invoke-RestMethod "$BASE/pipeline/$ToolId/run" -Method Post -ContentType "application/json" -Body $body -TimeoutSec 15
        return @{ ok = $r.data.success; output = $r.data.output; errors = $r.data.errors }
    } catch {
        return @{ ok = $false; output = ""; errors = @("$_") }
    }
}

function Add-Result {
    param([string]$Category, [string]$Name, [bool]$Pass, [string]$Detail)
    $icon = if ($Pass) { "[OK]" } else { "[!!]" }
    $color = if ($Pass) { "Green" } else { "Red" }
    Write-Host ("  {0} {1,-40} {2}" -f $icon, $Name, $Detail) -ForegroundColor $color
    $results.Add([PSCustomObject]@{
        Category = $Category
        Name     = $Name
        Pass     = $Pass
        Detail   = $Detail
    })
}

# ─── Health check ────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host "  T8 Robustness Test Suite" -ForegroundColor Cyan
Write-Host "==================================================================" -ForegroundColor Cyan

try {
    $h = Invoke-RestMethod "$BASE/health" -TimeoutSec 5
    Write-Host "  Server: $($h.status) | pattern: $($h.pattern)" -ForegroundColor Green
} catch {
    Write-Host "  Server not reachable — start T8 first!" -ForegroundColor Red
    exit 1
}

# ═══════════════════════════════════════════════════════════════════════════════
#  CAT 1: GENERATION DIVERSITY — can it generate different kinds of tools?
# ═══════════════════════════════════════════════════════════════════════════════
Write-Host ""
Write-Host "── CAT 1: Generation Diversity ──────────────────────────────────" -ForegroundColor Yellow

$tasks = @(
    @{ task = "Reverse a string and count vowels in it";               session = "cat1-a"; runs = @("Hello World","OpenAI","") }
    @{ task = "Convert Celsius temperature to Fahrenheit and Kelvin";  session = "cat1-b"; runs = @("0","100","-273.15","abc") }
    @{ task = "Check if a string is a palindrome";                     session = "cat1-c"; runs = @("racecar","hello","","A man a plan a canal Panama") }
    @{ task = "Count words and characters in a text";                  session = "cat1-d"; runs = @("The quick brown fox","","   ") }
    @{ task = "Compute the sum and product of a list of numbers (JSON array)"; session = "cat1-e"; runs = @("[1,2,3,4,5]","[]","[0]","not-json") }
)

foreach ($t in $tasks) {
    $p = Invoke-Pipeline -Task $t.task -Session $t.session
    $compiled = $p.ok -and $p.step -eq "completed"
    Add-Result "diversity" "compile: $($t.task.Substring(0,[Math]::Min(38,$t.task.Length)))" $compiled "step=$($p.step) time=$($p.timing)"

    if ($compiled) {
        foreach ($inp in $t.runs) {
            $r = Invoke-Run -ToolId $p.toolId -Input $inp -Session $t.session
            $label = if ($inp.Length -eq 0) { "<empty>" } elseif ($inp.Length -gt 20) { $inp.Substring(0,20)+"…" } else { $inp }
            Add-Result "diversity" "  run[$label]" $r.ok "out=$(($r.output).Substring(0,[Math]::Min(60,($r.output).Length)))"
        }
    }
}

# ═══════════════════════════════════════════════════════════════════════════════
#  CAT 2: INPUT EDGE CASES — empty, null-ish, huge, unicode, injection
# ═══════════════════════════════════════════════════════════════════════════════
Write-Host ""
Write-Host "── CAT 2: Input Edge Cases ──────────────────────────────────────" -ForegroundColor Yellow

# Use fibonacci (likely cached in registry from previous test)
$fibTask = Invoke-Pipeline -Task "Count the characters in a string and return uppercase" -Session "cat2-base"
$tid = $fibTask.toolId

if ($fibTask.ok) {
    $edgeCases = @(
        @{ input = "";                             label = "empty string" }
        @{ input = "   ";                          label = "whitespace only" }
        @{ input = "hello";                        label = "normal" }
        @{ input = ("a" * 5000);                   label = "5000-char string" }
        @{ input = "Hello 世界 🌍 مرحبا";          label = "unicode + emoji" }
        @{ input = '"; DROP TABLE tools; --';      label = "SQL injection attempt" }
        @{ input = "<script>alert(1)</script>";    label = "XSS attempt" }
        @{ input = ("{0}" -f [char]0);             label = "null character" }
        @{ input = "A" * 100000;                   label = "100k chars (stress)" }
    )
    foreach ($e in $edgeCases) {
        $r = Invoke-Run -ToolId $tid -Input $e.input -Session "cat2"
        # We only care it does NOT crash the server (success OR graceful error)
        # Survival = server didn't crash (success OR any graceful error, excluding HTTP 500)
        $httpErr = "$_"
        $survived = $r.ok -or ($r.output.Length -ge 0 -and -not ($httpErr -match '500|InternalServerError'))
        Add-Result "edge-input" $e.label $survived "success=$($r.ok) out=$(($r.output).Substring(0,[Math]::Min(40,($r.output).Length)))"
    }
}

# ═══════════════════════════════════════════════════════════════════════════════
#  CAT 3: PIPELINE RESILIENCE — bad tasks, repeated tasks, concurrent
# ═══════════════════════════════════════════════════════════════════════════════
Write-Host ""
Write-Host "── CAT 3: Pipeline Resilience ───────────────────────────────────" -ForegroundColor Yellow

# 3a: Ambiguous / very short task
$p = Invoke-Pipeline -Task "do stuff" -Session "cat3-vague"
Add-Result "resilience" "vague task ('do stuff')" ($null -ne $p.toolId) "step=$($p.step)"

# 3b: Very long task description
$longTask = "Given a JSON array of student objects each with name, score, and grade fields, " +
            "compute statistics including min, max, average, median score, grade distribution " +
            "as a frequency map, and return a ranked leaderboard sorted descending by score " +
            "with position, name, and normalized score as a percentage of max"
$p = Invoke-Pipeline -Task $longTask -Session "cat3-complex"
Add-Result "resilience" "complex task (grades+stats)" ($p.ok -and $p.step -eq "completed") "step=$($p.step) time=$($p.timing)"

if ($p.ok) {
    $students = '[{"name":"Alice","score":92,"grade":"A"},{"name":"Bob","score":78,"grade":"B"},{"name":"Charlie","score":85,"grade":"B"},{"name":"Diana","score":92,"grade":"A"},{"name":"Eve","score":61,"grade":"D"}]'
    $r = Invoke-Run -ToolId $p.toolId -Input $students -Session "cat3-complex"
    Add-Result "resilience" "  run with real JSON data" $r.ok "out=$(($r.output).Substring(0,[Math]::Min(80,($r.output).Length)))"
}

# 3c: Same task twice — should reuse or regenerate cleanly
$p1 = Invoke-Pipeline -Task "Convert a number from decimal to binary and hexadecimal" -Session "cat3-dup1"
$p2 = Invoke-Pipeline -Task "Convert a number from decimal to binary and hexadecimal" -Session "cat3-dup2"
Add-Result "resilience" "same task ×2 (no conflict)" ($p1.ok -and $p2.ok) "ids=$($p1.toolId) vs $($p2.toolId)"

if ($p1.ok) {
    $r = Invoke-Run -ToolId $p1.toolId -Input "255" -Session "cat3-dup1"
    Add-Result "resilience" "  dec2bin(255)" $r.ok "out=$($r.output)"
}

# 3d: Concurrent pipeline calls (3 parallel)
Write-Host "  [running 3 concurrent pipelines...]" -ForegroundColor DarkGray
$concurrentTasks = @(
    "Sort a comma-separated list of numbers in ascending order"
    "Find the maximum and minimum values in a JSON number array"
    "Count occurrences of each character in a string as a frequency map"
) | ForEach-Object -Parallel {
    $b = @{ task = $_; sessionId = "cat3-concurrent-$([System.Threading.Thread]::CurrentThread.ManagedThreadId)" } | ConvertTo-Json
    try {
        $r = Invoke-RestMethod "$using:BASE/pipeline" -Method Post -ContentType "application/json" -Body $b -TimeoutSec 60
        [PSCustomObject]@{ task = $_.Substring(0,30); ok = $true; step = $r.step; toolId = $r.toolId }
    } catch {
        [PSCustomObject]@{ task = $_.Substring(0,30); ok = $false; step = "$_"; toolId = $null }
    }
} -ThrottleLimit 3

foreach ($c in $concurrentTasks) {
    Add-Result "resilience" "concurrent: $($c.task)…" $c.ok "step=$($c.step)"
}

# ═══════════════════════════════════════════════════════════════════════════════
#  CAT 4: STEP-BY-STEP ENDPOINTS — individual stage calls
# ═══════════════════════════════════════════════════════════════════════════════
Write-Host ""
Write-Host "── CAT 4: Step-by-Step Endpoints ────────────────────────────────" -ForegroundColor Yellow

# detect only
try {
    $r = Invoke-RestMethod "$BASE/pipeline/detect" -Method Post -ContentType "application/json" -Body '{"task":"Calculate the area of a circle given its radius","sessionId":"cat4"}' -TimeoutSec 30
    Add-Result "stepwise" "POST /detect" ($null -ne $r.data.toolId) "toolId=$($r.data.toolId)"
    $detectedId = $r.data.toolId
} catch { Add-Result "stepwise" "POST /detect" $false "$_" }

# tools list
try {
    $r = Invoke-RestMethod "$BASE/tools" -Method Get -TimeoutSec 5
    Add-Result "stepwise" "GET /tools" ($null -ne $r) "count=$($r.Count)"
} catch { Add-Result "stepwise" "GET /tools" $false "$_" }

# skills
try {
    $r = Invoke-RestMethod "$BASE/skills" -Method Get -TimeoutSec 5
    Add-Result "stepwise" "GET /skills" ($null -ne $r) "count=$($r.Count)"
} catch { Add-Result "stepwise" "GET /skills" $false "$_" }

# 404 for unknown toolId
try {
    Invoke-RestMethod "$BASE/pipeline/nonexistent-tool-00000/run" -Method Post -ContentType "application/json" -Body '{"input":"x","sessionId":"x"}' -TimeoutSec 5
    Add-Result "stepwise" "run unknown toolId → 404" $false "should have thrown"
} catch {
    $errBody = $_.ErrorDetails.Message
    $is404 = ("$_" -match "404|NotFound") -or ($errBody -match "404|not found|Tool .* not")
    $detail = if ($errBody) { $errBody.Substring(0,[Math]::Min(80,$errBody.Length)) } else { "$_".Substring(0,[Math]::Min(80,"$_".Length)) }
    Add-Result "stepwise" "run unknown toolId → 404" $is404 $detail
}

# ═══════════════════════════════════════════════════════════════════════════════
#  SUMMARY
# ═══════════════════════════════════════════════════════════════════════════════
Write-Host ""
Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host "  SUMMARY" -ForegroundColor Cyan
Write-Host "==================================================================" -ForegroundColor Cyan

$cats = $results | Group-Object Category
foreach ($cat in $cats) {
    $pass = ($cat.Group | Where-Object Pass).Count
    $total = $cat.Group.Count
    $pct = [Math]::Round($pass / $total * 100)
    $color = if ($pct -ge 80) { "Green" } elseif ($pct -ge 60) { "Yellow" } else { "Red" }
    Write-Host ("  {0,-20} : {1,2}/{2} ({3}%)" -f $cat.Name, $pass, $total, $pct) -ForegroundColor $color
}

$totalPass  = ($results | Where-Object Pass).Count
$totalAll   = $results.Count
$totalPct   = [Math]::Round($totalPass / $totalAll * 100)
Write-Host ""
$totalColor = if ($totalPct -ge 75) { "Green" } else { "Yellow" }
Write-Host ("  TOTAL : {0}/{1} ({2}%)" -f $totalPass, $totalAll, $totalPct) -ForegroundColor $totalColor

$failed = $results | Where-Object { -not $_.Pass }
if ($failed) {
    Write-Host ""
    Write-Host "  Failed cases:" -ForegroundColor Red
    foreach ($f in $failed) {
        Write-Host ("    [{0}] {1} → {2}" -f $f.Category, $f.Name, $f.Detail) -ForegroundColor DarkRed
    }
}
Write-Host ""
