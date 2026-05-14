#!/usr/bin/env bash
# ── agxioma-research-agent: setup + demo ─────────────────────────────────────

set -euo pipefail
PROJ="AgxiomaResearchAgent.csproj"

echo "==> Initialising user-secrets for $PROJ"
dotnet user-secrets init --project "$PROJ"

echo ""
echo "Choose your LLM provider:"
echo "  1) OpenAI     (gpt-4o orchestration + gpt-4o-mini workers)"
echo "  2) Anthropic  (claude-3-7 orchestration + claude-haiku workers)"
echo "  3) Ollama     (llama3.2 — no API key)"
read -r -p "Provider [1]: " choice
choice="${choice:-1}"

case "$choice" in
  1)
    read -r -p "OpenAI API key: " key
    dotnet user-secrets set "LLMProvider:OpenAI:ApiKey" "$key" --project "$PROJ"
    echo "✔ OpenAI key saved."
    ;;
  2)
    read -r -p "Anthropic API key: " key
    dotnet user-secrets set "LLMProvider:Anthropic:ApiKey" "$key" --project "$PROJ"
    echo "✔ Anthropic key saved."
    ;;
  3)
    echo "No API key needed for Ollama."
    echo "Ensure Ollama is running: ollama serve"
    ;;
esac

echo ""
echo "==> Done! Run with:"
echo "    dotnet run --project $PROJ"
echo ""
echo "==> Quick demo (async — fire and poll):"
echo ""
echo "# Start research job:"
echo '    JOB=$(curl -s -X POST http://localhost:5003/research \'
echo '      -H "Content-Type: application/json" \'
echo '      -d '"'"'{"topic":"Impact of large language models on software engineering productivity in 2024-2025"}'"'"' | jq -r .jobId)'
echo '    echo "Job ID: $JOB"'
echo ""
echo "# Poll until done:"
echo '    watch -n 5 "curl -s http://localhost:5003/research/$JOB | jq .status"'
echo ""
echo "==> Or use the synchronous endpoint (blocks until complete):"
echo '    curl -s -X POST http://localhost:5003/research/sync \'
echo '      -H "Content-Type: application/json" \'
echo '      -d '"'"'{"topic":"What are the pros and cons of microservices vs monolithic architecture?"}'"'"' | jq .synthesis'
