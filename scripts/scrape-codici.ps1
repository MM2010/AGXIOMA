$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"
$baseUrl = "https://www.brocardi.it"
$delayMs = 1200; $timeout = 30
$civilDir = "data/codice-civile"; $penalDir = "data/codice-penale"
$null = New-Item -ItemType Directory -Path $civilDir -Force
$null = New-Item -ItemType Directory -Path $penalDir -Force

# REORDERED: most important first (torts, contracts, property before marriage)
$civilPaths = @(
    "codice-civile/libro-quarto/titolo-ix",      # 2043-2059 fatti illeciti (TOP PRIORITY)
    "codice-civile/libro-quarto/titolo-i",        # 1173-1320 obbligazioni
    "codice-civile/libro-quarto/titolo-ii",       # 1321-1469 contratti
    "codice-civile/libro-terzo/titolo-ii",        # 832-951 proprietà
    "codice-civile/libro-terzo/titolo-viii",      # 1140-1170 possesso
    "codice-civile/libro-sesto/titolo-v",         # 2934-2969 prescrizione
    "codice-civile/libro-sesto/titolo-ii",        # 2697-2739 prove
    "codice-civile/libro-primo/titolo-i",         # 1-10 persone
    "codice-civile/libro-primo/titolo-ii",        # 11-42 persone giuridiche
    "codice-civile/libro-primo/titolo-vi"         # 79-230 matrimonio (last)
)
$penalPaths = @(
    "codice-penale/libro-secondo/titolo-xii/capo-i",      # 575-593 delitti contro vita
    "codice-penale/libro-secondo/titolo-xii/capo-iii",    # 600-609 libertà individuale
    "codice-penale/libro-secondo/titolo-xiii/capo-i",     # 624-635 furto, rapina
    "codice-penale/libro-secondo/titolo-xiii/capo-ii",    # 636-649 altri delitti patrimonio
    "codice-penale/libro-primo/titolo-i",         # 1-16 legge penale
    "codice-penale/libro-primo/titolo-ii",        # 17-38 pene
    "codice-penale/libro-primo/titolo-iii",       # 39-84 reato
    "codice-penale/libro-primo/titolo-iv",        # 85-131 reo e offesa
    "codice-penale/libro-primo/titolo-vi"         # 150-184 estinzione
)
$totalCivil = 0; $totalPenal = 0; $civilLimit = 110; $penalLimit = 110

function Fetch-Page($url){
    try{ $r = Invoke-WebRequest -Uri $url -TimeoutSec $timeout -UseBasicParsing -ErrorAction Stop; return $r.Content }
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

Write-Host "`n==== CODICE CIVILE (riordinato: priorità a fatti illeciti, obbligazioni, proprietà) ====" -ForegroundColor Yellow
foreach($tp in $civilPaths){
    if($totalCivil -ge $civilLimit){ break }
    $html = Fetch-Page "https://www.brocardi.it/$tp/"
    if(-not $html){ continue }
    $urls = Get-ArticleUrls $html
    Write-Host "$(Get-Date -Format HH:mm:ss) $($urls.Count) articoli in $tp" -ForegroundColor Cyan
    foreach($au in $urls){
        if($totalCivil -ge $civilLimit){ break }
        $ah = Fetch-Page "https://www.brocardi.it$au"
        if(-not $ah){ continue }
        $t = Get-Title $ah; $x = Extract-Text $ah
        if($x){ Save-Article $civilDir $au $t $x "codice-civile"; $totalCivil++ }
        else{ Write-Host "$(Get-Date -Format HH:mm:ss) NO TEXT $au" -ForegroundColor Yellow }
        Start-Sleep -Milliseconds $delayMs
    }
}
Write-Host "`n==== CODICE PENALE ====" -ForegroundColor Yellow
foreach($tp in $penalPaths){
    if($totalPenal -ge $penalLimit){ break }
    $html = Fetch-Page "https://www.brocardi.it/$tp/"
    if(-not $html){ continue }
    $urls = Get-ArticleUrls $html
    Write-Host "$(Get-Date -Format HH:mm:ss) $($urls.Count) articoli in $tp" -ForegroundColor Cyan
    foreach($au in $urls){
        if($totalPenal -ge $penalLimit){ break }
        $ah = Fetch-Page "https://www.brocardi.it$au"
        if(-not $ah){ continue }
        $t = Get-Title $ah; $x = Extract-Text $ah
        if($x){ Save-Article $penalDir $au $t $x "codice-penale"; $totalPenal++ }
        else{ Write-Host "$(Get-Date -Format HH:mm:ss) NO TEXT $au" -ForegroundColor Yellow }
        Start-Sleep -Milliseconds $delayMs
    }
}
Write-Host "`n==== REPORT ====" -ForegroundColor Yellow
Write-Host "CC: $totalCivil articoli" -ForegroundColor Green
Write-Host "CP: $totalPenal articoli" -ForegroundColor Green
Write-Host "Salvati in $civilDir/ e $penalDir/" -ForegroundColor Cyan
