#!/usr/bin/env bash
# ── agxioma-code-reviewer: setup + demo ──────────────────────────────────────

set -euo pipefail
PROJ="AgxiomaCodeReviewer.csproj"

echo "==> Initialising user-secrets for $PROJ"
dotnet user-secrets init --project "$PROJ"

echo ""
read -r -p "OpenAI/Anthropic API key: " key
dotnet user-secrets set "LLMProvider:OpenAI:ApiKey" "$key" --project "$PROJ"
echo "✔ Key saved."

echo ""
echo "==> Run with:  dotnet run --project $PROJ"
echo ""
echo "==> Demo — review a code snippet:"
cat << 'DEMO'
    curl -s -X POST http://localhost:5004/review \
      -H "Content-Type: application/json" \
      -d '{
        "language": "csharp",
        "context": "Production API endpoint — auth is critical",
        "code": "public async Task<IActionResult> Login(string user, string pwd) {\n  var q = $\"SELECT * FROM users WHERE name='"'"'{user}'"'"' AND pass='"'"'{pwd}'"'"'\";\n  var r = await _db.QueryAsync(q);\n  if (r.Any()) { HttpContext.Session.SetString(\"user\", user); return Ok(); }\n  return Unauthorized();\n}"
      }' | jq .finalReport
DEMO
echo ""
echo "==> Demo — review a local file:"
echo '    curl -s -X POST http://localhost:5004/review/file \'
echo '      -H "Content-Type: application/json" \'
echo '      -d '"'"'{"filePath":"/path/to/MyService.cs","context":"Core business logic"}'"'"' | jq .finalReport'
echo ""
echo "==> Demo — review a git diff:"
echo '    git diff HEAD~1 HEAD | curl -s -X POST http://localhost:5004/review/diff \'
echo '      -H "Content-Type: application/json" \'
echo '      -d @- <<'"'"'EOF'"'"
echo '{"diff":"'"'"'$(cat)'"'"'","prTitle":"Fix authentication bug","description":"Replaces raw SQL with parameterised queries"}'
echo 'EOF'
