#!/usr/bin/env bash
# ── agxioma-dynamic-skill-lab: setup + demo ──────────────────────────────────
# Sistema avanzato di runtime self-extension:
# rileva capability mancanti → genera tool C# → valida → compila Roslyn → esegue in sandbox

set -euo pipefail
PROJ="AgxiomaDynamicSkillLab.csproj"

echo "==> Initialising user-secrets for $PROJ"
dotnet user-secrets init --project "$PROJ"

read -r -p "API key (OpenAI/Anthropic): " key
dotnet user-secrets set "LLMProvider:OpenAI:ApiKey" "$key" --project "$PROJ"
echo "✔ Key saved."

echo ""
echo "==> Run with:  dotnet run --project $PROJ"
echo ""
echo "==> Demo — FULL PIPELINE (detect → generate → analyze → compile → run):"
echo ""
echo "    curl -s -X POST http://localhost:5007/pipeline \\"
echo '      -H "Content-Type: application/json" \'
echo '      -d '"'"'{"task":"Analyze these Nginx logs and identify anomalous traffic patterns: 192.168.1.1 - - [10/May/2026:13:45:12 +0000] \"GET /wp-admin/admin-ajax.php HTTP/1.1\" 200 532 \"-\" \"Mozilla/5.0\"\n192.168.1.2 - - [10/May/2026:13:45:13 +0000] \"GET /index.html HTTP/1.1\" 200 1234 \"-\" \"Mozilla/5.0\"\n192.168.1.1 - - [10/May/2026:13:45:14 +0000] \"POST /xmlrpc.php HTTP/1.1\" 200 89 \"-\" \"python-requests/2.28\"\n192.168.1.1 - - [10/May/2026:13:45:15 +0000] \"POST /xmlrpc.php HTTP/1.1\" 200 89 \"-\" \"python-requests/2.28\"\n10.0.0.5 - - [10/May/2026:13:45:16 +0000] \"GET /robots.txt HTTP/1.1\" 404 162 \"-\" \"Googlebot/2.1\""}'"'" | jq "{status, step, toolName, data: {execution}}'"
echo ""
echo "==> Demo — STEP-BY-STEP:"
echo ""
echo "# 1. Detect capability gap:"
echo '    curl -s -X POST http://localhost:5007/pipeline/detect \'
echo '      -H "Content-Type: application/json" \'
echo '      -d '"'"'{"task":"Extract all email addresses from a large text corpus and check for duplicates"}'"'"' | jq .'
echo ""
echo "# 2. Generate tool:"
echo '    curl -s -X POST http://localhost:5007/pipeline/generate \'
echo '      -H "Content-Type: application/json" \'
echo '      -d '"'"'{"toolId":"email-extractor","toolName":"email-extractor","description":"Extracts email addresses from text, detects duplicates","taskContext":"Parse text input, find all email@ patterns using regex, return unique + duplicate counts","sessionId":"demo-session"}'"'"' | jq "{status, step, data: {manifest}}"' 
echo ""
echo "# 3. List generated tools:"
echo '    curl -s http://localhost:5007/tools | jq .'
echo ""
echo "# 4. List promoted skills:"
echo '    curl -s http://localhost:5007/skills | jq .'
echo ""
echo "# 5. Get audit trail:"
echo '    curl -s http://localhost:5007/audit/demo-session | jq .'
echo ""
echo "# 6. View active (loaded) runtime tools:"
echo '    curl -s http://localhost:5007/tools/active | jq .'
