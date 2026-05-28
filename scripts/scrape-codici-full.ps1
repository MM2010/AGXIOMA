#!/usr/bin/env pwsh
$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"
$civilDir = "data/codice-civile"
$penalDir = "data/codice-penale"
$null = New-Item -ItemType Directory -Path $civilDir -Force
$null = New-Item -ItemType Directory -Path $penalDir -Force

# ── TUTTI i titoli del Codice Civile ──
$civilTitles = @(
    "codice-civile/libro-primo/titolo-i","codice-civile/libro-primo/titolo-ii","codice-civile/libro-primo/titolo-iii",
    "codice-civile/libro-primo/titolo-iv","codice-civile/libro-primo/titolo-v","codice-civile/libro-primo/titolo-vi",
    "codice-civile/libro-primo/titolo-vii","codice-civile/libro-primo/titolo-viii","codice-civile/libro-primo/titolo-ix",
    "codice-civile/libro-primo/titolo-ix-bis","codice-civile/libro-primo/titolo-x","codice-civile/libro-primo/titolo-xi",
    "codice-civile/libro-primo/titolo-xii","codice-civile/libro-primo/titolo-xiii","codice-civile/libro-primo/titolo-xiv",
    "codice-civile/libro-secondo/titolo-i","codice-civile/libro-secondo/titolo-ii","codice-civile/libro-secondo/titolo-iii",
    "codice-civile/libro-secondo/titolo-iv","codice-civile/libro-secondo/titolo-v",
    "codice-civile/libro-terzo/titolo-i","codice-civile/libro-terzo/titolo-ii","codice-civile/libro-terzo/titolo-iii",
    "codice-civile/libro-terzo/titolo-iv","codice-civile/libro-terzo/titolo-v","codice-civile/libro-terzo/titolo-vi",
    "codice-civile/libro-terzo/titolo-vii","codice-civile/libro-terzo/titolo-viii","codice-civile/libro-terzo/titolo-ix",
    "codice-civile/libro-quarto/titolo-i","codice-civile/libro-quarto/titolo-ii","codice-civile/libro-quarto/titolo-iii",
    "codice-civile/libro-quarto/titolo-iv","codice-civile/libro-quarto/titolo-v","codice-civile/libro-quarto/titolo-vi",
    "codice-civile/libro-quarto/titolo-vii","codice-civile/libro-quarto/titolo-viii","codice-civile/libro-quarto/titolo-ix",
    "codice-civile/libro-quinto/titolo-i","codice-civile/libro-quinto/titolo-ii","codice-civile/libro-quinto/titolo-iii",
    "codice-civile/libro-quinto/titolo-iv","codice-civile/libro-quinto/titolo-v","codice-civile/libro-quinto/titolo-vi",
    "codice-civile/libro-quinto/titolo-vii","codice-civile/libro-quinto/titolo-viii","codice-civile/libro-quinto/titolo-ix",
    "codice-civile/libro-quinto/titolo-x","codice-civile/libro-quinto/titolo-xi",
    "codice-civile/libro-sesto/titolo-i","codice-civile/libro-sesto/titolo-ii","codice-civile/libro-sesto/titolo-iii",
    "codice-civile/libro-sesto/titolo-iv","codice-civile/libro-sesto/titolo-v"
)

# ── TUTTI i titoli del Codice Penale ──
$penalTitles = @(
    "codice-penale/libro-primo/titolo-i","codice-penale/libro-primo/titolo-ii","codice-penale/libro-primo/titolo-iii",
    "codice-penale/libro-primo/titolo-iv","codice-penale/libro-primo/titolo-v","codice-penale/libro-primo/titolo-vi",
    "codice-penale/libro-primo/titolo-vii","codice-penale/libro-primo/titolo-viii",
    "codice-penale/libro-secondo/titolo-i","codice-penale/libro-secondo/titolo-ii","codice-penale/libro-secondo/titolo-iii",
    "codice-penale/libro-secondo/titolo-iv","codice-penale/libro-secondo/titolo-v","codice-penale/libro-secondo/titolo-vi",
    "codice-penale/libro-secondo/titolo-vi-bis","codice-penale/libro-secondo/titolo-vii","codice-penale/libro-secondo/titolo-viii",
    "codice-penale/libro-secondo/titolo-viii-bis","codice-penale/libro-secondo/titolo-ix","codice-penale/libro-secondo/titolo-ix-bis",
    "codice-penale/libro-secondo/titolo-x","codice-penale/libro-secondo/titolo-xi","codice-penale/libro-secondo/titolo-xii",
    "codice-penale/libro-secondo/titolo-xiii",
    "codice-penale/libro-terzo/titolo-i","codice-penale/libro-terzo/titolo-ii","codice-penale/libro-terzo/titolo-ii-bis"
)

# ⚠ Titoli con capi: li aggiungiamo separatamente
$penalCapi = @(
    "codice-penale/libro-secondo/titolo-xii/capo-i","codice-penale/libro-secondo/titolo-xii/capo-ii",
    "codice-penale/libro-secondo/titolo-xii/capo-iii","codice-penale/libro-secondo/titolo-xii/capo-iv",
    "codice-penale/libro-secondo/titolo-xiii/capo-i","codice-penale/libro-secondo/titolo-xiii/capo-ii",
    "codice-penale/libro-secondo/titolo-xiii/capo-iii"
)

$civilLimit = 1200; $penalLimit = 1200
$totalCivil = 0; $totalPenal = 0
$delayMs = 800

function Fetch-Page($url){
    try{ $r = Invoke-WebRequest -Uri $url -TimeoutSec 30 -UseBasicParsing -ErrorAction Stop; return $r.Content }
    catch{ Write-Host "$(Get-Date -Format HH:mm:ss) 404 $url" -ForegroundColor Red; return $null }
}

function Get-ArticleUrls($html){
    $urls = @(); $r = [regex]'href="(/(?:codice-civile|codice-penale)/[^"]+/art\d+[a-z]*(?:bis|ter|quater|quinquies|sexies)?\.html)"'
    $m = $r.Matches($html)
    foreach($x in $m){ $urls += $x.Groups[1].Value }
    $seen = @{}; $result = @()
    foreach($u in $urls){ if(-not $seen.ContainsKey($u)){ $seen[$u]=$true; $result+=$u } }
    return $result
}

function Extract-Text($html){
    $m=[regex]::Match($html,'(?s)Dispositivo\s+dell[^<]*art\.\s*\d+[^<]*(.*?)(?:<h[23]|Ratio\s+Legis|Brocardi|Spiegazione|Relazione|Massime|Notizie|Abbiamo\s+a\s+cuore)')
    if($m.Success){ $t=$m.Groups[1].Value; $t=[regex]::Replace($t,'<[^>]+>',''); $t=[regex]::Replace($t,'\s+',' '); return $t.Trim() }
    # fallback
    $m2=[regex]::Match($html,'(?s)(?:Dispositivo).*?<p[^>]*>(.*?)(?:</(?:p|div)>).*?(?:<h[23]|Note|Ratio|Massime|Abbiamo)')
    if($m2.Success){ $t=[regex]::Replace($m2.Groups[1].Value,'<[^>]+>',''); $t=[regex]::Replace($t,'\s+',' '); return $t.Trim() }
    return $null
}

function Get-Title($html){
    $m=[regex]::Match($html,'<title>(.*?)</title>')
    if($m.Success){ return ($m.Groups[1].Value -replace '\s*\|.*','').Trim() }
    return "Articolo"
}

function Save-Article($dir, $slug, $title, $txt, $cat){
    $slug2 = $slug -replace '.*/art', 'art' -replace '\.html$',''
    $fpath = Join-Path $dir "$slug2.md"
    $md = "---`nslug: $slug2`ntitle: $title`ncategory: $cat`n---`n`n$txt"
    [System.IO.File]::WriteAllText($fpath, $md, [System.Text.Encoding]::UTF8)
    Write-Host "$(Get-Date -Format HH:mm:ss) OK $slug2.md" -ForegroundColor Green
}

function Process-Code($name, $paths, $extraPaths, $dir, $limit, [ref]$counter){
    Write-Host "`n==== $name ====" -ForegroundColor Yellow
    $allPaths = $paths + $extraPaths
    foreach($tp in $allPaths){
        if($counter.Value -ge $limit){ Write-Host "Limite $limit raggiunto per $name, stop." -ForegroundColor Magenta; break }
        $html = Fetch-Page "https://www.brocardi.it/$tp/"
        if(-not $html){ continue }
        $urls = Get-ArticleUrls $html
        Write-Host "$(Get-Date -Format HH:mm:ss) $($urls.Count) articoli in $tp" -ForegroundColor Cyan
        foreach($au in $urls){
            if($counter.Value -ge $limit){ break }
            $ah = Fetch-Page "https://www.brocardi.it$au"
            if(-not $ah){ continue }
            $t = Get-Title $ah; $x = Extract-Text $ah
            if($x){ Save-Article $dir $au $t $x $name.Substring(0,4).ToLower(); $counter.Value++ }
            else{ Write-Host "$(Get-Date -Format HH:mm:ss) NO TEXT $au" -ForegroundColor Yellow }
            Start-Sleep -Milliseconds $delayMs
        }
    }
}

Process-Code "CODICE CIVILE" $civilTitles @() $civilDir $civilLimit ([ref]$totalCivil)
Process-Code "CODICE PENALE" $penalTitles $penalCapi $penalDir $penalLimit ([ref]$totalPenal)

Write-Host "`n==== REPORT FINALE ====" -ForegroundColor Yellow
Write-Host "CC: $totalCivil articoli" -ForegroundColor Green
Write-Host "CP: $totalPenal articoli" -ForegroundColor Green
Write-Host "Salvati in data/codice-civile/ e data/codice-penale/" -ForegroundColor Cyan
