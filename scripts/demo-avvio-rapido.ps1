@'
╔══════════════════════════════════════════════════════════════════╗
║   AGXIOME — Studio Legale Demo — Avvio Rapido                  ║
║   19 Maggio 2026                                               ║
╚══════════════════════════════════════════════════════════════════╝

PREREQUISITI:
  - Docker Desktop (in esecuzione)
  - .NET 10 SDK
  - Node.js 20+
  - Ollama con mxbai-embed-large (opzionale per embedding)

PASSO 1 — Avvia infrastruttura Docker
  docker compose -f docker-compose.legal-email.yml up -d
  (PostgreSQL su :5433, Redis su :6380)

PASSO 2 — Imposta la chiave DeepSeek
  $env:DEEPSEEK_API_KEY = "sk-e233360b9fd24494861ee970e96fefc5"

PASSO 3 — Avvia il backend
  cd apps\agxioma-legal-email
  dotnet run
  (all'avvio fa ingestion automatica dei codici in pgvector)
  (ascolta su http://localhost:5013)

PASSO 4 — Avvia il frontend (altro terminale)
  cd apps\agxioma-legal-email\frontend
  npm run dev
  (apre http://localhost:3001)

TEST RAPIDI:
  # Health check
  curl http://localhost:5013/health

  # Stato ingestion
  curl http://localhost:5013/api/legal/ingestion-status

  # Query codici
  curl "http://localhost:5013/api/legal/query?q=Art.%202043%20codice%20civile"

  # Invia email demo
  curl -X POST http://localhost:5013/api/email/ingest ^
    -H "Content-Type: application/json" ^
    -d "{\"sender\":\"cliente@example.com\",\"subject\":\"Furto in appartamento\",\"body\":\"Buongiorno, ho subito un furto in appartamento la settimana scorsa. Vorrei sapere quali sono i miei diritti.\"}"

  # Verifica pipeline
  curl http://localhost:5013/api/email/{EMAIL_ID}/pipeline

  # Approva bozza
  curl -X POST http://localhost:5013/api/review/approve ^
    -H "Content-Type: application/json" ^
    -d "{\"emailId\":\"{EMAIL_ID}\",\"lawyerId\":\"avv-001\"}"

  # Dashboard
  curl http://localhost:5013/api/dashboard
'@ | ForEach-Object { Write-Host $_ -ForegroundColor Cyan }