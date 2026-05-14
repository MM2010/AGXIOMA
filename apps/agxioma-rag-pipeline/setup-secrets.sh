#!/usr/bin/env bash
# ── agxioma-rag-pipeline: setup + demo ───────────────────────────────────────
# Stores API keys in dotnet user-secrets (never committed to git).

set -euo pipefail
PROJ="AgxiomaRagPipeline.csproj"

echo "==> Initialising user-secrets for $PROJ"
dotnet user-secrets init --project "$PROJ"

echo ""
echo "Choose embedder + LLM provider:"
echo "  1) OpenAI  (text-embedding-3-small + gpt-4o)"
echo "  2) Anthropic (text-embedding-3-small + claude-3-7)"
echo "  3) Ollama  (nomic-embed-text + llama3.2 — no API key)"
read -r -p "Provider [1]: " choice
choice="${choice:-1}"

case "$choice" in
  1|2)
    read -r -p "OpenAI API key (used for both embeddings and chat): " key
    dotnet user-secrets set "LLMProvider:OpenAI:ApiKey" "$key" --project "$PROJ"
    if [[ "$choice" == "2" ]]; then
      read -r -p "Anthropic API key: " akey
      dotnet user-secrets set "LLMProvider:Anthropic:ApiKey" "$akey" --project "$PROJ"
    fi
    echo "✔ Keys saved."
    ;;
  3)
    echo "No API key needed for Ollama."
    echo "Ensure Ollama is running and nomic-embed-text is pulled:"
    echo "  ollama pull nomic-embed-text"
    echo "  ollama pull llama3.2"
    ;;
esac

echo ""
echo "==> Done! Run the pipeline with:"
echo "    dotnet run --project $PROJ"
echo ""
echo "==> Quick demo (after starting the server):"
echo ""
echo "# 1. Ingest your docs folder:"
echo '    curl -s -X POST http://localhost:5002/ingest \'
echo '      -H "Content-Type: application/json" \'
echo '      -d '"'"'{"path":"/path/to/docs"}'"'"' | jq .'
echo ""
echo "# 2. Ingest raw text:"
echo '    curl -s -X POST http://localhost:5002/ingest/text \'
echo '      -H "Content-Type: application/json" \'
echo '      -d '"'"'{"id":"hello-world","content":"AGXIOME is a .NET 10 AI framework."}'"'"' | jq .'
echo ""
echo "# 3. Query the knowledge base:"
echo '    curl -s -X POST http://localhost:5002/query \'
echo '      -H "Content-Type: application/json" \'
echo '      -d '"'"'{"question":"What is AGXIOME?","topK":3}'"'"' | jq .'
echo ""
echo "# 4. List collections:"
echo "    curl -s http://localhost:5002/collections | jq ."
