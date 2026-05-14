$messages = @(
    # BILLING (15)
    @{ id=1;  cat="billing";   msg="I have a question about my invoice from last month" },
    @{ id=2;  cat="billing";   msg="How do I request a refund for an incorrect charge?" },
    @{ id=3;  cat="billing";   msg="I want to cancel my subscription immediately" },
    @{ id=4;  cat="billing";   msg="Can I get a receipt for my last payment?" },
    @{ id=5;  cat="billing";   msg="The billing amount seems wrong, I was charged twice" },
    @{ id=6;  cat="billing";   msg="How does proration work when I upgrade mid-cycle?" },
    @{ id=7;  cat="billing";   msg="I need a tax certificate for my company's accounting" },
    @{ id=8;  cat="billing";   msg="Can I change my payment method from credit card to bank transfer?" },
    @{ id=9;  cat="billing";   msg="I have a discount coupon, how do I apply it?" },
    @{ id=10; cat="billing";   msg="What is the billing cycle for the annual plan?" },
    @{ id=11; cat="billing";   msg="I want to downgrade my plan to save costs" },
    @{ id=12; cat="billing";   msg="My credit card was declined but the service is still active?" },
    @{ id=13; cat="billing";   msg="Do you offer any discounts for non-profit organizations?" },
    @{ id=14; cat="billing";   msg="I need an invoice with my company VAT number" },
    @{ id=15; cat="billing";   msg="How much would I pay if I add 5 more users to my subscription?" },

    # TECHNICAL (15)
    @{ id=16; cat="technical"; msg="I keep getting a 401 authentication error on your API" },
    @{ id=17; cat="technical"; msg="The application crashes when I try to upload files larger than 10MB" },
    @{ id=18; cat="technical"; msg="How do I configure the SSL certificate for my custom domain?" },
    @{ id=19; cat="technical"; msg="I'm getting a timeout exception when calling the endpoint" },
    @{ id=20; cat="technical"; msg="The installation fails with error code 0x80070005 on Windows 11" },
    @{ id=21; cat="technical"; msg="My API token stopped working after the latest update" },
    @{ id=22; cat="technical"; msg="The build pipeline fails at the deploy step with a connection error" },
    @{ id=23; cat="technical"; msg="How do I debug the stack trace from the exception in production?" },
    @{ id=24; cat="technical"; msg="The response time is very slow, sometimes over 30 seconds" },
    @{ id=25; cat="technical"; msg="I have a bug: the search feature returns wrong results after the v2.3 update" },
    @{ id=26; cat="technical"; msg="How do I configure the API endpoint for the European region?" },
    @{ id=27; cat="technical"; msg="Getting an SSL handshake failure when connecting from our server" },
    @{ id=28; cat="technical"; msg="The webhook is not receiving events even though the URL is correct" },
    @{ id=29; cat="technical"; msg="How do I integrate your API with Python using OAuth2?" },
    @{ id=30; cat="technical"; msg="The version 3.1 update broke our existing configuration, how to rollback?" },

    # SALES (10)
    @{ id=31; cat="sales";     msg="What features are included in the Enterprise plan?" },
    @{ id=32; cat="sales";     msg="Can we extend our trial for another 30 days to finish evaluation?" },
    @{ id=33; cat="sales";     msg="I want to compare your product with competitors, can you help?" },
    @{ id=34; cat="sales";     msg="We need a formal quote for 500 licenses for our company" },
    @{ id=35; cat="sales";     msg="Do you have a reseller or partner program I can join?" },
    @{ id=36; cat="sales";     msg="Can we schedule a product demo for my management team?" },
    @{ id=37; cat="sales";     msg="We want to do a proof of concept before purchasing, what's the process?" },
    @{ id=38; cat="sales";     msg="Do you offer an SLA with 99.99% uptime guarantee for enterprise?" },
    @{ id=39; cat="sales";     msg="What is the difference between the Pro plan and Business plan?" },
    @{ id=40; cat="sales";     msg="We are ready to buy 200 licenses, can we negotiate a volume discount?" },

    # GENERAL (10)
    @{ id=41; cat="general";   msg="Hello, I need some help getting started" },
    @{ id=42; cat="general";   msg="What are your support hours?" },
    @{ id=43; cat="general";   msg="How do I reset my password?" },
    @{ id=44; cat="general";   msg="Where can I find the documentation?" },
    @{ id=45; cat="general";   msg="Can you explain how the platform works in general?" },
    @{ id=46; cat="general";   msg="I'm new here, what should I do first?" },
    @{ id=47; cat="general";   msg="Is there a mobile app available?" },
    @{ id=48; cat="general";   msg="When will the next maintenance window be?" },
    @{ id=49; cat="general";   msg="How do I contact a human agent?" },
    @{ id=50; cat="general";   msg="Tell me what your product does" }
)

Write-Host "Sending 50 messages in parallel to T6 ForestSwarm..." -ForegroundColor Cyan
Write-Host ("=" * 70)

$results = $messages | ForEach-Object -Parallel {
    $m = $_
    try {
        $body = @{ message = $m.msg; sessionId = "test-$($m.id)" } | ConvertTo-Json
        $r = Invoke-RestMethod "http://localhost:5005/support" -Method Post `
            -ContentType "application/json" -Body $body -TimeoutSec 60
        [PSCustomObject]@{
            Id         = $m.id
            Expected   = $m.cat
            RoutedTo   = $r.teamCategory
            Agent      = $r.routedTo
            Keywords   = ($r.matchedKeywords -join ", ")
            Duration   = $r.duration
            Correct    = ($r.teamCategory -eq $m.cat)
            Message    = $m.msg.Substring(0, [Math]::Min(55, $m.msg.Length))
        }
    } catch {
        [PSCustomObject]@{
            Id         = $m.id
            Expected   = $m.cat
            RoutedTo   = "ERROR"
            Agent      = "ERROR"
            Keywords   = $_.Exception.Message.Substring(0, [Math]::Min(40, $_.Exception.Message.Length))
            Duration   = "N/A"
            Correct    = $false
            Message    = $m.msg.Substring(0, [Math]::Min(55, $m.msg.Length))
        }
    }
} -ThrottleLimit 10 | Sort-Object Id

Write-Host ""
Write-Host "RESULTS:" -ForegroundColor Yellow
Write-Host ("=" * 70)

$results | ForEach-Object {
    $color = if ($_.Correct) { "Green" } else { "Red" }
    $tick  = if ($_.Correct) { "[OK]" } else { "[!!]" }
    Write-Host ("{0} #{1:D2} exp={2,-10} got={3,-10} kw=[{4}] {5}" -f `
        $tick, $_.Id, $_.Expected, $_.RoutedTo, $_.Keywords, $_.Duration) -ForegroundColor $color
}

Write-Host ""
Write-Host "SUMMARY:" -ForegroundColor Yellow
Write-Host ("=" * 70)

$correct = ($results | Where-Object Correct).Count
$total   = $results.Count
$pct     = [Math]::Round($correct / $total * 100, 1)

Write-Host "Total:   $total" -ForegroundColor White
Write-Host "Correct: $correct / $total  ($pct%)" -ForegroundColor $(if ($pct -ge 80) { "Green" } else { "Yellow" })

Write-Host ""
Write-Host "By category:" -ForegroundColor Cyan
foreach ($cat in @("billing","technical","sales","general")) {
    $catItems   = $results | Where-Object { $_.Expected -eq $cat }
    $catCorrect = ($catItems | Where-Object Correct).Count
    $catTotal   = $catItems.Count
    Write-Host ("  {0,-10}: {1}/{2}" -f $cat, $catCorrect, $catTotal) -ForegroundColor $(if ($catCorrect -eq $catTotal) {"Green"} else {"Yellow"})
}

Write-Host ""
Write-Host "Misrouted messages:" -ForegroundColor Red
$results | Where-Object { -not $_.Correct } | ForEach-Object {
    Write-Host ("  #{0:D2} expected={1} got={2} -> '{3}'" -f $_.Id, $_.Expected, $_.RoutedTo, $_.Message) -ForegroundColor Red
}
