#!/usr/bin/env pwsh
<#
.SYNOPSIS
  Test pilot per la demo Studio Legale AGXIOME
  Invia 3 email di esempio e verifica il flusso completo
#>

$BASE = "http://localhost:5013"
$ErrorActionPreference = "Stop"

function Test-Step($name, $scriptBlock) {
    Write-Host "`n▶ $name..." -ForegroundColor Yellow
    try { & $scriptBlock; Write-Host "  ✅ OK" -ForegroundColor Green }
    catch { Write-Host "  ❌ $($_.Exception.Message)" -ForegroundColor Red }
}

# Step 1: Health
Test-Step "Health check" {
    $r = Invoke-WebRequest -Uri "$BASE/health" -UseBasicParsing
    if ($r.StatusCode -ne 200) { throw "Health fallito: $($r.StatusCode)" }
    $body = $r.Content | ConvertFrom-Json
    Write-Host "  Provider: $($body.provider), Model: $($body.model)" -ForegroundColor Gray
}

# Step 2: Ingestion status
Test-Step "Stato ingestion" {
    $r = Invoke-WebRequest -Uri "$BASE/api/legal/ingestion-status" -UseBasicParsing
    $body = $r.Content | ConvertFrom-Json
    Write-Host "  Ingestito: $($body.ingested), Chunks: $($body.chunks)" -ForegroundColor Gray
    if (-not $body.ingested) { Write-Host "  ⚠  Ingestion in corso o non disponibile (attendi e riprova)" -ForegroundColor Yellow }
}

# Step 3: Query legale
Test-Step "Query: Art. 2043 CC" {
    $q = [System.Web.HttpUtility]::UrlEncode("Art. 2043 codice civile risarcimento fatto illecito")
    $r = Invoke-WebRequest -Uri "$BASE/api/legal/query?q=$q" -UseBasicParsing
    $body = $r.Content | ConvertFrom-Json
    Write-Host "  Risposta: $(if($body.contextFound){"✅ trovata"}else{"⚠️ non disponibile (ingestion in corso?)"})" -ForegroundColor Gray
}

# Step 4: Invia email — furto
Test-Step "Invia email: Furto in appartamento" {
    $body = @{sender="mario.rossi@example.com"; subject="Furto in appartamento"; body="Buongiorno, la settimana scorsa dei ladri sono entrati nel mio appartamento mentre ero fuori. Hanno rubato gioielli e un computer. Ho già sporto denuncia in commissariato. Vorrei capire quali sono i miei diritti e come procedere per il risarcimento."} | ConvertTo-Json
    $r = Invoke-WebRequest -Uri "$BASE/api/email/ingest" -Method Post -Body $body -ContentType "application/json" -UseBasicParsing
    $res = $r.Content | ConvertFrom-Json
    Write-Host "  EmailId: $($res.emailId)" -ForegroundColor Cyan
    $script:emailId1 = $res.emailId
}

# Step 5: Invia email — divorzio
Test-Step "Invia email: Separazione" {
    $body = @{sender="laura.bianchi@example.com"; subject="Separazione coniugale"; body="Buongiorno, mio marito ed io abbiamo deciso di separarci consensualmente. Abbiamo due figli minori e una casa di proprietà. Vorrei sapere come funziona l'affidamento e la divisione dei beni."} | ConvertTo-Json
    $r = Invoke-WebRequest -Uri "$BASE/api/email/ingest" -Method Post -Body $body -ContentType "application/json" -UseBasicParsing
    $res = $r.Content | ConvertFrom-Json
    Write-Host "  EmailId: $($res.emailId)" -ForegroundColor Cyan
    $script:emailId2 = $res.emailId
}

# Step 6: Invia email — licenziamento
Test-Step "Invia email: Licenziamento" {
    $body = @{sender="giuseppe.verdi@example.com"; subject="Licenziamento ingiusto"; body="Salve, sono stato licenziato senza preavviso dopo 5 anni di lavoro. L'azienda dice che è per riduzione del personale ma credo sia un pretesto. Ho diritto a qualcosa? Posso impugnare il licenziamento?"} | ConvertTo-Json
    $r = Invoke-WebRequest -Uri "$BASE/api/email/ingest" -Method Post -Body $body -ContentType "application/json" -UseBasicParsing
    $res = $r.Content | ConvertFrom-Json
    Write-Host "  EmailId: $($res.emailId)" -ForegroundColor Cyan
    $script:emailId3 = $res.emailId
}

# Step 7: Verifica pipeline prima email
Start-Sleep -Seconds 3
Test-Step "Verifica pipeline prima email" {
    $r = Invoke-WebRequest -Uri "$BASE/api/email/$emailId1/pipeline" -UseBasicParsing
    $res = $r.Content | ConvertFrom-Json
    Write-Host "  Stato: $($res.state)" -ForegroundColor Gray
    foreach ($t in $res.timeline) {
        $dur = if ($t.duration) { " ($([math]::Round($t.duration/1000,1))s)" } else { "" }
        Write-Host "    $($t.phase)${dur}" -ForegroundColor DarkGray
    }
}

# Step 8: Dashboard
Test-Step "Dashboard" {
    $r = Invoke-WebRequest -Uri "$BASE/api/dashboard" -UseBasicParsing
    $res = $r.Content | ConvertFrom-Json
    Write-Host "  Email: $($res.totalEmails), In HITL: $($res.inHitl), Completate: $($res.completed)" -ForegroundColor Gray
}

# Step 9: Approva prima email
Test-Step "Approva prima email" {
    $body = @{emailId=$emailId1; lawyerId="avv-001"} | ConvertTo-Json
    $r = Invoke-WebRequest -Uri "$BASE/api/review/approve" -Method Post -Body $body -ContentType "application/json" -UseBasicParsing
    $res = $r.Content | ConvertFrom-Json
    if ($res.status -ne "approved") { throw "Approvazione fallita" }
    Write-Host "  ✅ Approvata: $emailId1" -ForegroundColor Green
}

# Step 10: Dashboard finale
Test-Step "Dashboard finale" {
    $r = Invoke-WebRequest -Uri "$BASE/api/dashboard" -UseBasicParsing
    $res = $r.Content | ConvertFrom-Json
    Write-Host "  Email: $($res.totalEmails), In HITL: $($res.inHitl), Completate: $($res.completed)" -ForegroundColor Gray
}

Write-Host "`n══════════ TEST COMPLETATO ══════════" -ForegroundColor Yellow
Write-Host "Apri http://localhost:3001 per la UI" -ForegroundColor Cyan
