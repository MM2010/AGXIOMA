#!/usr/bin/env bash
# ── agxioma-tool-agent: API key setup ────────────────────────────────────────
# Stores API keys in dotnet user-secrets (never committed to git).

set -euo pipefail
PROJ="AgxiomaToolAgent.csproj"

echo "==> Initialising user-secrets for $PROJ"
dotnet user-secrets init --project "$PROJ"

echo ""
echo "Choose your LLM provider:"
echo "  1) OpenAI"
echo "  2) Anthropic"
echo "  3) Ollama (no key required)"
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
    echo "No API key needed for Ollama — make sure Ollama is running on port 11434."
    ;;
  *)
    echo "Unknown choice. Exiting."
    exit 1
    ;;
esac

echo ""
echo "==> Done! Run the agent with:"
echo "    dotnet run --project $PROJ"
echo ""
echo "==> Try it:"
echo '    curl -s -X POST http://localhost:5001/chat \'
echo '      -H "Content-Type: application/json" \'
echo '      -d '"'"'{"prompt":"Search the web: what is DuckDuckGo?"}'"'"' | jq .'
echo ""
echo '    curl -s -X POST http://localhost:5001/chat \'
echo '      -H "Content-Type: application/json" \'
echo '      -d '"'"'{"prompt":"Read this file and summarise it: /path/to/notes.md"}'"'"' | jq .'
