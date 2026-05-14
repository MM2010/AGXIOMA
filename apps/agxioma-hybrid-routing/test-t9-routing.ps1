# ─────────────────────────────────────────────────────────────────────────────
# T9 — HybridSemanticRouting Exhaustive Test
# 100 routing questions via POST /route/diagnose (no LLM calls — fast)
# Expected: billing(25) | technical(25) | sales(25) | general(25)
# ─────────────────────────────────────────────────────────────────────────────

$base = "http://localhost:5008"

# ─── Wait for server ──────────────────────────────────────────────────────────
Write-Host ""
Write-Host "T9 — HybridSemanticRouting — 100-Question Exhaustive Test" -ForegroundColor Cyan
Write-Host "=" * 60 -ForegroundColor DarkGray
Write-Host "Waiting for server at $base..." -ForegroundColor Yellow

$ready = $false
for ($i = 0; $i -lt 20; $i++) {
    try {
        $h = Invoke-RestMethod "$base/health" -Method Get -TimeoutSec 3 -ErrorAction Stop
        Write-Host "Server ready — routing mode: $($h.routingMode), embedding: $($h.embeddingAvailable)" -ForegroundColor Green
        $ready = $true
        break
    } catch {
        Start-Sleep -Milliseconds 1500
    }
}

if (-not $ready) {
    Write-Host "ERROR: Server did not start. Run:" -ForegroundColor Red
    Write-Host "  cd d:\REPO_PERSONALI\AGXIOMA\AGXIOMA\apps\agxioma-hybrid-routing" -ForegroundColor Yellow
    Write-Host "  `$env:DEEPSEEK_API_KEY='sk-116c138368374a3b9013cc9cb3e052ce'" -ForegroundColor Yellow
    Write-Host "  dotnet run" -ForegroundColor Yellow
    exit 1
}

Write-Host ""

# ─── Test cases ───────────────────────────────────────────────────────────────
# Format: @{ msg = "..."; expected = "billing|technical|sales|general" }

$tests = @(
    # ── BILLING (25) ──────────────────────────────────────────────────────────
    @{ msg = "I was charged twice this month";                     expected = "billing" },
    @{ msg = "How do I cancel my subscription?";                   expected = "billing" },
    @{ msg = "I need a refund for last month's payment";           expected = "billing" },
    @{ msg = "Can you send me an invoice for Q1?";                 expected = "billing" },
    @{ msg = "My payment failed, what should I do?";               expected = "billing" },
    @{ msg = "How do I update my credit card on file?";            expected = "billing" },
    @{ msg = "I need a tax receipt for my last purchase";          expected = "billing" },
    @{ msg = "What payment methods do you accept?";                expected = "billing" },
    @{ msg = "I was charged the wrong amount";                     expected = "billing" },
    @{ msg = "When will my refund be processed?";                  expected = "billing" },
    @{ msg = "My subscription expired but I was still charged";    expected = "billing" },
    @{ msg = "Can I get a proration credit for unused time?";      expected = "billing" },
    @{ msg = "I want to downgrade and get a credit back";          expected = "billing" },
    @{ msg = "How do I apply a discount coupon to my account?";    expected = "billing" },
    @{ msg = "I want to cancel and get a full refund";             expected = "billing" },
    @{ msg = "The invoice shows the wrong tax amount";             expected = "billing" },
    @{ msg = "I need a VAT invoice for my company";                expected = "billing" },
    @{ msg = "Why was my credit card declined?";                   expected = "billing" },
    @{ msg = "Can I pay by bank transfer or wire?";                expected = "billing" },
    @{ msg = "I need a receipt for my expense report";             expected = "billing" },
    @{ msg = "My bill seems much higher than expected";            expected = "billing" },
    @{ msg = "Can I switch to annual billing and get a discount?"; expected = "billing" },
    @{ msg = "I was billed for a cancelled service";               expected = "billing" },
    @{ msg = "How do I update my billing address?";                expected = "billing" },
    @{ msg = "My subscription auto-renewed without any notice";    expected = "billing" },

    # ── TECHNICAL (25) ────────────────────────────────────────────────────────
    @{ msg = "API returning 401 unauthorized error";               expected = "technical" },
    @{ msg = "My webhook is not receiving any events";             expected = "technical" },
    @{ msg = "How do I authenticate with OAuth 2.0?";             expected = "technical" },
    @{ msg = "Getting a 500 internal server error on every request"; expected = "technical" },
    @{ msg = "My API token keeps expiring after 1 hour";          expected = "technical" },
    @{ msg = "Installation failed on Windows 11";                  expected = "technical" },
    @{ msg = "Configuration file is not being loaded correctly";   expected = "technical" },
    @{ msg = "Getting timeout errors on all API calls";            expected = "technical" },
    @{ msg = "SSL certificate error when connecting to endpoint";  expected = "technical" },
    @{ msg = "My deployment broke after the latest release";       expected = "technical" },
    @{ msg = "Stack trace shows NullPointerException in production"; expected = "technical" },
    @{ msg = "API rate limit being exceeded consistently";         expected = "technical" },
    @{ msg = "WebSocket connection keeps dropping every 30 seconds"; expected = "technical" },
    @{ msg = "Database connection timeout after 5 seconds";        expected = "technical" },
    @{ msg = "Build fails with missing dependency error";          expected = "technical" },
    @{ msg = "Authentication token becomes invalid after refresh"; expected = "technical" },
    @{ msg = "API endpoint returning 403 forbidden for all users"; expected = "technical" },
    @{ msg = "Requests are extremely slow, taking 30 seconds each"; expected = "technical" },
    @{ msg = "Getting CORS errors in the browser console";         expected = "technical" },
    @{ msg = "My logs show continuous exception errors";           expected = "technical" },
    @{ msg = "How do I debug API request failures?";               expected = "technical" },
    @{ msg = "Integration failing with connection refused error";  expected = "technical" },
    @{ msg = "Webhook signature validation is failing";            expected = "technical" },
    @{ msg = "Can't connect to the API endpoint at all";           expected = "technical" },
    @{ msg = "How do I rotate my API keys securely?";              expected = "technical" },

    # ── SALES (25) ────────────────────────────────────────────────────────────
    @{ msg = "What enterprise plans do you offer?";                expected = "sales" },
    @{ msg = "I want to schedule a product demo";                  expected = "sales" },
    @{ msg = "How does pricing work for large teams?";             expected = "sales" },
    @{ msg = "Can I get a custom enterprise contract?";            expected = "sales" },
    @{ msg = "We need licenses for 500 users";                     expected = "sales" },
    @{ msg = "What features are included in the Pro plan?";        expected = "sales" },
    @{ msg = "How does the free trial work?";                      expected = "sales" },
    @{ msg = "Can we negotiate a volume discount?";                expected = "sales" },
    @{ msg = "I want to compare your pricing plans";               expected = "sales" },
    @{ msg = "Do you have startup pricing programs?";              expected = "sales" },
    @{ msg = "Can you extend our trial for another month?";        expected = "sales" },
    @{ msg = "What's included in the enterprise tier?";            expected = "sales" },
    @{ msg = "Do you offer SLA guarantees in contracts?";          expected = "sales" },
    @{ msg = "We want to run a proof of concept first";            expected = "sales" },
    @{ msg = "Can we get a reseller or partner agreement?";        expected = "sales" },
    @{ msg = "What are the differences between your plans?";       expected = "sales" },
    @{ msg = "How do I upgrade to the Professional tier?";         expected = "sales" },
    @{ msg = "Do you offer non-profit or NGO pricing?";            expected = "sales" },
    @{ msg = "We need an on-premise deployment option";            expected = "sales" },
    @{ msg = "Can I get a quote for 100 seats?";                   expected = "sales" },
    @{ msg = "We want to run a 30-day pilot program";              expected = "sales" },
    @{ msg = "What's the process to purchase enterprise?";         expected = "sales" },
    @{ msg = "Do you offer academic institution discounts?";       expected = "sales" },
    @{ msg = "I want to request a product walkthrough meeting";    expected = "sales" },
    @{ msg = "We are comparing you against your competitors";      expected = "sales" },

    # ── GENERAL (25) ──────────────────────────────────────────────────────────
    @{ msg = "Hello, how do I get started?";                       expected = "general" },
    @{ msg = "What is this product exactly?";                      expected = "general" },
    @{ msg = "Can you help me with something?";                    expected = "general" },
    @{ msg = "I have a general question";                          expected = "general" },
    @{ msg = "How does the mobile app work?";                      expected = "general" },
    @{ msg = "Where can I find the documentation?";                expected = "general" },
    @{ msg = "Is there a status page for service outages?";        expected = "general" },
    @{ msg = "How do I contact support?";                          expected = "general" },
    @{ msg = "What are your business hours?";                      expected = "general" },
    @{ msg = "Can I speak to a human agent?";                      expected = "general" },
    @{ msg = "I want to change my account settings";               expected = "general" },
    @{ msg = "How do I update my profile information?";            expected = "general" },
    @{ msg = "Where do I find tutorials and guides?";              expected = "general" },
    @{ msg = "Is there planned maintenance tonight?";              expected = "general" },
    @{ msg = "Can you explain how this feature works?";            expected = "general" },
    @{ msg = "I'm new here, where should I start?";                expected = "general" },
    @{ msg = "What's new in the latest product update?";           expected = "general" },
    @{ msg = "How do I reset my password?";                        expected = "general" },
    @{ msg = "What languages is the product available in?";        expected = "general" },
    @{ msg = "I need help with my account";                        expected = "general" },
    @{ msg = "Can you walk me through the basics?";                expected = "general" },
    @{ msg = "Where is the user guide or manual?";                 expected = "general" },
    @{ msg = "How do I navigate the main dashboard?";              expected = "general" },
    @{ msg = "What third-party integrations do you support?";      expected = "general" },
    @{ msg = "Hi there!";                                          expected = "general" }
)

# ─── Run tests ────────────────────────────────────────────────────────────────
$results = @()
$categories = @{}
$confDist = @{ High = 0; Medium = 0; Low = 0 }

Write-Host "Running $($tests.Count) routing tests..." -ForegroundColor Cyan
Write-Host ""

foreach ($t in $tests) {
    $body = @{ message = $t.msg; sessionId = "test-$(Get-Random)" } | ConvertTo-Json

    try {
        $r = Invoke-RestMethod "$base/route/diagnose" `
            -Method Post `
            -ContentType "application/json" `
            -Body $body `
            -TimeoutSec 10 `
            -ErrorAction Stop

        $pass = $r.selectedTree -eq $t.expected

        $results += [PSCustomObject]@{
            Msg        = $t.msg
            Expected   = $t.expected
            Got        = $r.selectedTree
            Score      = $r.selectedScore
            Confidence = $r.confidence
            Ambiguous  = $r.isAmbiguous
            EmbUsed    = $r.embeddingUsed
            Pass       = $pass
        }

        if (-not $categories.ContainsKey($t.expected)) {
            $categories[$t.expected] = @{ Pass = 0; Fail = 0 }
        }
        if ($pass) { $categories[$t.expected].Pass++ }
        else        { $categories[$t.expected].Fail++ }

        $confDist[$r.confidence]++

        $color = if ($pass) { "Green" } else { "Red" }
        $icon  = if ($pass) { "PASS" } else { "FAIL" }
        Write-Host "[$icon] [$($t.expected.ToUpper().PadRight(9))] $($t.msg.Substring(0, [Math]::Min(55, $t.msg.Length)).PadRight(55)) → $($r.selectedTree) (score=$($r.selectedScore.ToString('F3')), $($r.confidence))" `
            -ForegroundColor $color

    } catch {
        $errMsg = $_.ErrorDetails.Message
        if (-not $errMsg) { $errMsg = $_.Exception.Message }

        $results += [PSCustomObject]@{
            Msg        = $t.msg
            Expected   = $t.expected
            Got        = "ERROR"
            Score      = 0
            Confidence = "N/A"
            Ambiguous  = $false
            EmbUsed    = $false
            Pass       = $false
        }
        if (-not $categories.ContainsKey($t.expected)) {
            $categories[$t.expected] = @{ Pass = 0; Fail = 0 }
        }
        $categories[$t.expected].Fail++
        Write-Host "[FAIL] [$($t.expected.ToUpper().PadRight(9))] $($t.msg.Substring(0, [Math]::Min(55, $t.msg.Length)).PadRight(55)) → ERROR: $errMsg" -ForegroundColor Red
    }
}

# ─── Summary ──────────────────────────────────────────────────────────────────

Write-Host ""
Write-Host "=" * 60 -ForegroundColor DarkGray
Write-Host "RESULTS SUMMARY" -ForegroundColor Cyan
Write-Host "=" * 60 -ForegroundColor DarkGray

$total      = $results.Count
$totalPass  = ($results | Where-Object { $_.Pass }).Count
$totalFail  = $total - $totalPass
$pct        = [Math]::Round(($totalPass / $total) * 100, 1)

Write-Host ""
Write-Host "Category Results:" -ForegroundColor White
foreach ($cat in @("billing", "technical", "sales", "general")) {
    if ($categories.ContainsKey($cat)) {
        $p = $categories[$cat].Pass
        $f = $categories[$cat].Fail
        $cp = [Math]::Round(($p / ($p + $f)) * 100, 1)
        $catColor = if ($f -eq 0) { "Green" } elseif ($cp -ge 80) { "Yellow" } else { "Red" }
        Write-Host "  $($cat.ToUpper().PadRight(10)) : $p/$(p+f) ($cp%)" -ForegroundColor $catColor
    }
}

Write-Host ""
Write-Host "Confidence Distribution:" -ForegroundColor White
Write-Host "  High   : $($confDist.High)" -ForegroundColor Green
Write-Host "  Medium : $($confDist.Medium)" -ForegroundColor Yellow
Write-Host "  Low    : $($confDist.Low)" -ForegroundColor Red

Write-Host ""
$totalColor = if ($pct -ge 90) { "Green" } elseif ($pct -ge 75) { "Yellow" } else { "Red" }
Write-Host "TOTAL: $totalPass/$total ($pct%)" -ForegroundColor $totalColor

if ($totalFail -gt 0) {
    Write-Host ""
    Write-Host "Failed tests ($totalFail):" -ForegroundColor Red
    $results | Where-Object { -not $_.Pass } | ForEach-Object {
        Write-Host "  [FAIL] Expected=[$($_.Expected)] Got=[$($_.Got)] Score=$($_.Score.ToString('F3')) : $($_.Msg)" -ForegroundColor Red
    }
}

Write-Host ""
if ($pct -ge 90) {
    Write-Host "T9 PASSED — HybridSemanticRouting validated ($pct%)" -ForegroundColor Green
} else {
    Write-Host "T9 NEEDS WORK — $pct% accuracy (target: >=90%)" -ForegroundColor Red
}
Write-Host ""
