#!/usr/bin/env bash
# ── agxioma-multi-agent-team: setup + demo ───────────────────────────────────

set -euo pipefail
PROJ="AgxiomaMultiAgentTeam.csproj"

echo "==> Initialising user-secrets for $PROJ"
dotnet user-secrets init --project "$PROJ"

read -r -p "API key (OpenAI/Anthropic): " key
dotnet user-secrets set "LLMProvider:OpenAI:ApiKey" "$key" --project "$PROJ"
echo "✔ Key saved."

echo ""
echo "==> Run with:  dotnet run --project $PROJ"
echo ""
echo "==> Demo — auto-routing:"
echo ""
echo "# → routes to BILLING:"
echo '    curl -s -X POST http://localhost:5005/support \'
echo '      -H "Content-Type: application/json" \'
echo '      -d '"'"'{"message":"I was charged twice for my subscription this month"}'"'"' | jq "{routedTo, teamCategory, answer}"'
echo ""
echo "# → routes to TECHNICAL:"
echo '    curl -s -X POST http://localhost:5005/support \'
echo '      -H "Content-Type: application/json" \'
echo '      -d '"'"'{"message":"Getting 401 Unauthorized when calling the API with my token"}'"'"' | jq "{routedTo, matchedKeywords, answer}"'
echo ""
echo "# → routes to SALES:"
echo '    curl -s -X POST http://localhost:5005/support \'
echo '      -H "Content-Type: application/json" \'
echo '      -d '"'"'{"message":"What are the differences between the Enterprise and Pro plans?"}'"'"' | jq "{routedTo, answer}"'
echo ""
echo "# → force routing to a team:"
echo '    curl -s -X POST http://localhost:5005/support/billing \'
echo '      -H "Content-Type: application/json" \'
echo '      -d '"'"'{"message":"I need help with my account"}'"'"' | jq .routedTo'
echo ""
echo "# → view routing map:"
echo '    curl -s http://localhost:5005/routing-map | jq .'
