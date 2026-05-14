#!/usr/bin/env bash
# ── agxioma-stock-tracker: setup + demo ──────────────────────────────────────

set -euo pipefail
PROJ="AgxiomaStockTracker.csproj"

echo "==> Initialising user-secrets for $PROJ"
dotnet user-secrets init --project "$PROJ"

read -r -p "API key (OpenAI/Anthropic): " key
dotnet user-secrets set "LLMProvider:OpenAI:ApiKey" "$key" --project "$PROJ"
echo "✔ Key saved."

echo ""
echo "==> Run with:  dotnet run --project $PROJ"
echo ""
echo "==> Demo flows:"
echo ""
echo "# Health check:"
echo '    curl -s http://localhost:5006/health | jq .'
echo ""
echo "# Get live quote + save to SQLite (routes to equity specialist):"
echo '    curl -s http://localhost:5006/quote/NVDA | jq .'
echo '    curl -s http://localhost:5006/quote/BTC-USD | jq .'
echo '    curl -s http://localhost:5006/quote/QQQ | jq .'
echo ""
echo "# Price history (SQLite, last 30 days):"
echo '    curl -s "http://localhost:5006/history/NVDA?days=7" | jq .'
echo ""
echo "# Deep analysis via ForestSwarm routing + HeavySwarm (async):"
echo '    JOB=$(curl -s -X POST http://localhost:5006/analyze/AAPL | jq -r .jobId)'
echo '    sleep 30'
echo '    curl -s "http://localhost:5006/analyze/AAPL/jobs/$JOB" | jq "{status, result: .result.synthesis}"'
echo ""
echo "# Grok debate — BUY / HOLD / SELL for ETH:"
echo '    curl -s -X POST http://localhost:5006/debate/ETH-USD | jq "{verdict, harperFindings}" '
echo ""
echo "# Watchlist (all tracked tickers):"
echo '    curl -s http://localhost:5006/watchlist | jq .'
