# ─────────────────────────────────────────────────────────────────────────────
# T9 — HybridSemanticRouting Massive Test — 500 questions
# 500 routing questions via POST /route/diagnose (no LLM calls — fast)
# Expected: billing(125) | technical(125) | sales(125) | general(125)
# ─────────────────────────────────────────────────────────────────────────────

$base = "http://localhost:5008"

Write-Host ""
Write-Host "T9 — HybridSemanticRouting — 500-Question Massive Test" -ForegroundColor Cyan
Write-Host ("=" * 60) -ForegroundColor DarkGray
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
    Write-Host "ERROR: Server not running." -ForegroundColor Red
    exit 1
}

Write-Host ""

# ─── Test cases ───────────────────────────────────────────────────────────────
$tests = @(

    # ════════════════════════════════════════════════════════════════════════
    # BILLING  (125)
    # ════════════════════════════════════════════════════════════════════════
    @{ msg = "I was charged twice this month";                                      expected = "billing" },
    @{ msg = "How do I cancel my subscription?";                                   expected = "billing" },
    @{ msg = "I need a refund for last month's payment";                           expected = "billing" },
    @{ msg = "Can you send me an invoice for Q1?";                                 expected = "billing" },
    @{ msg = "My payment failed, what should I do?";                               expected = "billing" },
    @{ msg = "How do I update my credit card on file?";                            expected = "billing" },
    @{ msg = "I need a tax receipt for my last purchase";                          expected = "billing" },
    @{ msg = "What payment methods do you accept?";                                expected = "billing" },
    @{ msg = "I was charged the wrong amount";                                     expected = "billing" },
    @{ msg = "When will my refund be processed?";                                  expected = "billing" },
    @{ msg = "My subscription expired but I was still charged";                    expected = "billing" },
    @{ msg = "Can I get a proration credit for unused time?";                      expected = "billing" },
    @{ msg = "I want to downgrade and get a credit back";                          expected = "billing" },
    @{ msg = "How do I apply a discount coupon to my account?";                    expected = "billing" },
    @{ msg = "I want to cancel and get a full refund";                             expected = "billing" },
    @{ msg = "The invoice shows the wrong tax amount";                             expected = "billing" },
    @{ msg = "I need a VAT invoice for my company";                                expected = "billing" },
    @{ msg = "Why was my credit card declined?";                                   expected = "billing" },
    @{ msg = "Can I pay by bank transfer or wire?";                                expected = "billing" },
    @{ msg = "I need a receipt for my expense report";                             expected = "billing" },
    @{ msg = "My bill seems much higher than expected";                            expected = "billing" },
    @{ msg = "Can I switch to annual billing and get a discount?";                 expected = "billing" },
    @{ msg = "I was billed for a cancelled service";                               expected = "billing" },
    @{ msg = "How do I update my billing address?";                                expected = "billing" },
    @{ msg = "My subscription auto-renewed without any notice";                    expected = "billing" },
    @{ msg = "I have a billing dispute I need resolved";                           expected = "billing" },
    @{ msg = "Can I pause my subscription instead of cancelling?";                 expected = "billing" },
    @{ msg = "I need to change my billing cycle from monthly to quarterly";        expected = "billing" },
    @{ msg = "How do I add a secondary payment method?";                           expected = "billing" },
    @{ msg = "My company needs a consolidated invoice for all seats";              expected = "billing" },
    @{ msg = "I accidentally purchased the wrong plan, can I get a refund?";       expected = "billing" },
    @{ msg = "Where can I download past invoices?";                                expected = "billing" },
    @{ msg = "I have not received my invoice for this month";                      expected = "billing" },
    @{ msg = "My coupon code is not working at checkout";                          expected = "billing" },
    @{ msg = "Can I apply a promo code retroactively?";                            expected = "billing" },
    @{ msg = "I need a refund because I never used the service";                   expected = "billing" },
    @{ msg = "What is your refund policy for annual plans?";                       expected = "billing" },
    @{ msg = "I was charged after I cancelled my account";                         expected = "billing" },
    @{ msg = "Please send a copy of invoice number 12345";                         expected = "billing" },
    @{ msg = "How do I get a receipt for a one-time purchase?";                    expected = "billing" },
    @{ msg = "I cannot find the charge on my credit card statement";               expected = "billing" },
    @{ msg = "My invoice is missing the company name";                             expected = "billing" },
    @{ msg = "Can I split the payment across two cards?";                          expected = "billing" },
    @{ msg = "I was overcharged for the premium tier";                             expected = "billing" },
    @{ msg = "How do I remove my payment method from the account?";                expected = "billing" },
    @{ msg = "I want to switch from credit card to direct debit";                  expected = "billing" },
    @{ msg = "The charge on my bank statement says unknown merchant";              expected = "billing" },
    @{ msg = "My PayPal payment is showing as pending";                            expected = "billing" },
    @{ msg = "I need a zero-rated VAT invoice for EU purchase";                    expected = "billing" },
    @{ msg = "Can I get an itemised billing breakdown?";                           expected = "billing" },
    @{ msg = "My organisation needs a purchase order number on invoices";          expected = "billing" },
    @{ msg = "I applied a voucher code but did not get the discount";              expected = "billing" },
    @{ msg = "Can you reissue an invoice with corrected billing details?";         expected = "billing" },
    @{ msg = "I am being charged for seats I deleted months ago";                  expected = "billing" },
    @{ msg = "My credit card expired; how do I avoid service interruption?";       expected = "billing" },
    @{ msg = "I downgraded but my next invoice still shows the higher price";      expected = "billing" },
    @{ msg = "Do you offer payment by invoice with 30-day terms?";                 expected = "billing" },
    @{ msg = "How do I dispute a charge I don't recognise?";                       expected = "billing" },
    @{ msg = "I want to know exactly what I am paying for each month";             expected = "billing" },
    @{ msg = "Can I get a tax-exempt invoice?";                                    expected = "billing" },
    @{ msg = "My bank declined the transaction; how do I fix this?";               expected = "billing" },
    @{ msg = "I need a formal receipt with a company stamp";                       expected = "billing" },
    @{ msg = "How do I cancel the auto-renewal on my account?";                    expected = "billing" },
    @{ msg = "I requested a refund 10 days ago and haven't heard back";            expected = "billing" },
    @{ msg = "What happens to my data if I don't pay my invoice?";                 expected = "billing" },
    @{ msg = "I'd like to settle the outstanding balance on my account";           expected = "billing" },
    @{ msg = "How do I add a billing contact email?";                              expected = "billing" },
    @{ msg = "My card was charged even though the payment failed message appeared"; expected = "billing" },
    @{ msg = "I need a GST invoice for tax purposes";                              expected = "billing" },
    @{ msg = "Can I use a prepaid Visa card for my subscription?";                 expected = "billing" },
    @{ msg = "I was charged a currency conversion fee I wasn't expecting";         expected = "billing" },
    @{ msg = "How do I freeze my subscription billing temporarily?";               expected = "billing" },
    @{ msg = "I need a duplicate receipt for a past payment";                      expected = "billing" },
    @{ msg = "The billing portal is not accepting my card";                        expected = "billing" },
    @{ msg = "I noticed a price increase I was not notified about";                expected = "billing" },
    @{ msg = "How do I set up recurring automatic payments?";                      expected = "billing" },
    @{ msg = "I want to add my company VAT number to my invoices";                 expected = "billing" },
    @{ msg = "My coupon has expired but I'd like to use it anyway";                expected = "billing" },
    @{ msg = "I cannot complete checkout because of a billing error";              expected = "billing" },
    @{ msg = "Do you charge in local currency or USD?";                            expected = "billing" },
    @{ msg = "How do I get a refund for an add-on I no longer need?";              expected = "billing" },
    @{ msg = "Can I get a partial refund for the remaining days of my plan?";      expected = "billing" },
    @{ msg = "I need billing statements for the past 12 months";                   expected = "billing" },
    @{ msg = "I was double-billed after changing plans";                           expected = "billing" },
    @{ msg = "My subscription renewed but I wanted to cancel";                     expected = "billing" },
    @{ msg = "How do I apply a redemption code I received by email?";              expected = "billing" },
    @{ msg = "Can I pay my overdue invoice online?";                               expected = "billing" },
    @{ msg = "I need an invoice sent to my accounts payable department";           expected = "billing" },
    @{ msg = "I received a payment confirmation but the invoice was never sent";   expected = "billing" },
    @{ msg = "How do I get a billing summary for my finance team?";                expected = "billing" },
    @{ msg = "I was charged a late payment fee I want waived";                     expected = "billing" },
    @{ msg = "Do you accept cryptocurrency as a payment method?";                  expected = "billing" },
    @{ msg = "Can I switch to quarterly billing mid-cycle?";                       expected = "billing" },
    @{ msg = "My invoice PDF is missing line items";                               expected = "billing" },
    @{ msg = "I want to pre-pay for 2 years and get a discount";                   expected = "billing" },
    @{ msg = "The checkout page keeps showing a payment error";                    expected = "billing" },
    @{ msg = "I did not authorise this charge; please investigate";                expected = "billing" },
    @{ msg = "Can I get a refund to store credit instead of back to my card?";     expected = "billing" },
    @{ msg = "I need to add a PO number to my next invoice";                       expected = "billing" },
    @{ msg = "The refund shows on your system but hasn't reached my bank";         expected = "billing" },
    @{ msg = "I was charged before my free trial ended";                           expected = "billing" },
    @{ msg = "How many days does it take to process a refund?";                    expected = "billing" },
    @{ msg = "I need a proforma invoice before I can approve the purchase";        expected = "billing" },
    @{ msg = "I see two separate charges for the same billing period";             expected = "billing" },
    @{ msg = "How do I update the email address where invoices are sent?";         expected = "billing" },
    @{ msg = "Can I pay in instalments instead of a lump sum?";                    expected = "billing" },
    @{ msg = "What is included in the enterprise billing package?";                expected = "billing" },
    @{ msg = "I received a billing notice but already cancelled last month";       expected = "billing" },
    @{ msg = "My bank flagged your charge as suspicious; how do I whitelist it?";  expected = "billing" },
    @{ msg = "I want to remove a seat and get a credit for the rest of the month"; expected = "billing" },
    @{ msg = "I applied a discount code but was still charged full price";         expected = "billing" },
    @{ msg = "Can I get a billing history export in CSV format?";                  expected = "billing" },
    @{ msg = "My subscription price changed without notice after an update";       expected = "billing" },
    @{ msg = "I need to change the billing contact on my account";                 expected = "billing" },
    @{ msg = "I received two invoices with the same invoice number";               expected = "billing" },
    @{ msg = "Do you offer a money-back guarantee?";                               expected = "billing" },
    @{ msg = "I want to close my account and request all charges be refunded";     expected = "billing" },
    @{ msg = "My payment was successful but my account is still locked";           expected = "billing" },
    @{ msg = "Can I use Apple Pay or Google Pay?";                                 expected = "billing" },
    @{ msg = "I need an invoice in a different currency";                          expected = "billing" },
    @{ msg = "My discount coupon is only applying to the first month";             expected = "billing" },
    @{ msg = "How do I enable automatic invoice sending to my accounting software?"; expected = "billing" },

    # ════════════════════════════════════════════════════════════════════════
    # TECHNICAL  (125)
    # ════════════════════════════════════════════════════════════════════════
    @{ msg = "API returning 401 unauthorized error";                               expected = "technical" },
    @{ msg = "My webhook is not receiving any events";                             expected = "technical" },
    @{ msg = "How do I authenticate with OAuth 2.0?";                             expected = "technical" },
    @{ msg = "Getting a 500 internal server error on every request";               expected = "technical" },
    @{ msg = "My API token keeps expiring after 1 hour";                           expected = "technical" },
    @{ msg = "Installation failed on Windows 11";                                  expected = "technical" },
    @{ msg = "Configuration file is not being loaded correctly";                   expected = "technical" },
    @{ msg = "Getting timeout errors on all API calls";                            expected = "technical" },
    @{ msg = "SSL certificate error when connecting to endpoint";                  expected = "technical" },
    @{ msg = "My deployment broke after the latest release";                       expected = "technical" },
    @{ msg = "Stack trace shows NullPointerException in production";               expected = "technical" },
    @{ msg = "API rate limit being exceeded consistently";                         expected = "technical" },
    @{ msg = "WebSocket connection keeps dropping every 30 seconds";               expected = "technical" },
    @{ msg = "Database connection timeout after 5 seconds";                        expected = "technical" },
    @{ msg = "Build fails with missing dependency error";                          expected = "technical" },
    @{ msg = "Authentication token becomes invalid after refresh";                 expected = "technical" },
    @{ msg = "API endpoint returning 403 forbidden for all users";                 expected = "technical" },
    @{ msg = "Requests are extremely slow, taking 30 seconds each";                expected = "technical" },
    @{ msg = "Getting CORS errors in the browser console";                         expected = "technical" },
    @{ msg = "My logs show continuous exception errors";                           expected = "technical" },
    @{ msg = "How do I debug API request failures?";                               expected = "technical" },
    @{ msg = "Integration failing with connection refused error";                  expected = "technical" },
    @{ msg = "Webhook signature validation is failing";                            expected = "technical" },
    @{ msg = "Can't connect to the API endpoint at all";                           expected = "technical" },
    @{ msg = "How do I rotate my API keys securely?";                              expected = "technical" },
    @{ msg = "My SDK throws an unhandled exception on initialisation";             expected = "technical" },
    @{ msg = "Getting a 429 too many requests error";                              expected = "technical" },
    @{ msg = "The REST endpoint returns 404 but the URL looks correct";            expected = "technical" },
    @{ msg = "How do I enable TLS 1.3 for all connections?";                       expected = "technical" },
    @{ msg = "My application crashes when calling the search API";                 expected = "technical" },
    @{ msg = "How do I implement exponential backoff for retries?";                expected = "technical" },
    @{ msg = "The webhook payload is malformed or truncated";                      expected = "technical" },
    @{ msg = "I'm getting a certificate chain validation error";                   expected = "technical" },
    @{ msg = "Environment variables are not being read by the app";                expected = "technical" },
    @{ msg = "My container keeps restarting with exit code 137";                   expected = "technical" },
    @{ msg = "How do I set up a reverse proxy for the API?";                       expected = "technical" },
    @{ msg = "The SDK documentation says method X exists but it throws";           expected = "technical" },
    @{ msg = "My Kubernetes pod fails health check after deploy";                  expected = "technical" },
    @{ msg = "How do I parse the nested JSON response from the API?";              expected = "technical" },
    @{ msg = "Getting memory leak warnings in the server logs";                    expected = "technical" },
    @{ msg = "The API returns different schemas in staging vs production";          expected = "technical" },
    @{ msg = "How do I add request signing to my API calls?";                      expected = "technical" },
    @{ msg = "My pagination is returning duplicate records";                       expected = "technical" },
    @{ msg = "OAuth redirect URI mismatch error during login";                     expected = "technical" },
    @{ msg = "The library version I'm using is deprecated; how do I upgrade?";     expected = "technical" },
    @{ msg = "How do I tail live application logs from the server?";               expected = "technical" },
    @{ msg = "The gRPC call returns deadline exceeded";                            expected = "technical" },
    @{ msg = "My CI/CD pipeline fails at the integration test step";               expected = "technical" },
    @{ msg = "I'm getting ECONNREFUSED when running locally";                      expected = "technical" },
    @{ msg = "How do I configure a custom DNS resolver for my service?";           expected = "technical" },
    @{ msg = "Getting null reference exception inside the callback";               expected = "technical" },
    @{ msg = "API gateway is stripping my custom headers";                         expected = "technical" },
    @{ msg = "How do I fix a deadlock in concurrent API calls?";                   expected = "technical" },
    @{ msg = "My Docker image build fails on the npm install step";                expected = "technical" },
    @{ msg = "The response body is always empty despite 200 status";               expected = "technical" },
    @{ msg = "How do I enable detailed error logging on the server?";              expected = "technical" },
    @{ msg = "My service mesh is dropping packets intermittently";                 expected = "technical" },
    @{ msg = "I keep getting 'invalid grant' from the OAuth server";               expected = "technical" },
    @{ msg = "How do I configure connection pooling for the database?";            expected = "technical" },
    @{ msg = "Getting JSON schema validation error on every request";              expected = "technical" },
    @{ msg = "My integration test suite takes 45 minutes; how to speed it up?";   expected = "technical" },
    @{ msg = "The file upload endpoint returns 413 payload too large";             expected = "technical" },
    @{ msg = "How do I enable HTTP/2 multiplexing?";                               expected = "technical" },
    @{ msg = "My scheduled job throws an exception every Tuesday";                 expected = "technical" },
    @{ msg = "Getting 'unable to verify the first certificate' SSL error";         expected = "technical" },
    @{ msg = "How do I trace a slow database query in production?";                expected = "technical" },
    @{ msg = "My load balancer is not distributing traffic evenly";                expected = "technical" },
    @{ msg = "The API client library leaks file descriptors";                      expected = "technical" },
    @{ msg = "How do I mock external API calls in my unit tests?";                 expected = "technical" },
    @{ msg = "Getting segmentation fault when processing large payloads";          expected = "technical" },
    @{ msg = "My feature flag service returns stale values after 1 minute";        expected = "technical" },
    @{ msg = "How do I enable GZIP compression on API responses?";                 expected = "technical" },
    @{ msg = "The CDN is caching responses that should be dynamic";                expected = "technical" },
    @{ msg = "My GraphQL subscription disconnects after 60 seconds";              expected = "technical" },
    @{ msg = "How do I handle race conditions in concurrent webhook processing?";  expected = "technical" },
    @{ msg = "I'm hitting the maximum open file descriptors limit";                expected = "technical" },
    @{ msg = "Getting 'broken pipe' errors on long-running requests";              expected = "technical" },
    @{ msg = "How do I set up mutual TLS between services?";                       expected = "technical" },
    @{ msg = "My API client is sending duplicate requests on timeout";             expected = "technical" },
    @{ msg = "The search index is returning outdated results";                     expected = "technical" },
    @{ msg = "How do I configure auto-scaling for my compute instances?";          expected = "technical" },
    @{ msg = "The API response is missing required fields in production only";     expected = "technical" },
    @{ msg = "Getting 'address already in use' when restarting the service";       expected = "technical" },
    @{ msg = "My async jobs are stuck in pending state";                           expected = "technical" },
    @{ msg = "How do I add distributed tracing to my microservices?";              expected = "technical" },
    @{ msg = "The server returns 502 bad gateway behind the load balancer";        expected = "technical" },
    @{ msg = "My cache invalidation is not working correctly";                     expected = "technical" },
    @{ msg = "How do I configure idle connection timeouts?";                       expected = "technical" },
    @{ msg = "Getting 'too many open connections' on the database";                expected = "technical" },
    @{ msg = "The batch API call returns partial results without explanation";      expected = "technical" },
    @{ msg = "How do I enable structured JSON logging in my application?";         expected = "technical" },
    @{ msg = "My blue/green deployment is failing the smoke test";                 expected = "technical" },
    @{ msg = "The API SDK is not serialising dates in the correct format";         expected = "technical" },
    @{ msg = "How do I configure health check endpoints for Kubernetes?";          expected = "technical" },
    @{ msg = "I'm getting inconsistent results from the same API call";            expected = "technical" },
    @{ msg = "My event listener is not firing for certain event types";            expected = "technical" },
    @{ msg = "How do I enable API request logging for audit purposes?";            expected = "technical" },
    @{ msg = "The service crashes when receiving a malformed request body";        expected = "technical" },
    @{ msg = "How do I set appropriate connection timeouts on the client side?";   expected = "technical" },
    @{ msg = "Getting 'connection reset by peer' during file downloads";           expected = "technical" },
    @{ msg = "My background worker is consuming 100% CPU unexpectedly";            expected = "technical" },
    @{ msg = "How do I implement idempotency keys for payment webhooks?";          expected = "technical" },
    @{ msg = "The API returns 200 but the operation was not performed";            expected = "technical" },
    @{ msg = "My Redis cache is evicting keys that should not be evicted";         expected = "technical" },
    @{ msg = "How do I safely roll back a failed database migration?";             expected = "technical" },
    @{ msg = "Getting HMAC signature mismatch on webhook verification";            expected = "technical" },
    @{ msg = "My serverless function times out after 15 seconds";                  expected = "technical" },
    @{ msg = "How do I configure request retry logic with jitter?";                expected = "technical" },
    @{ msg = "The multi-region setup has data inconsistencies between regions";    expected = "technical" },
    @{ msg = "Getting 'read timeout' on large file uploads";                       expected = "technical" },
    @{ msg = "How do I properly close idle HTTP keep-alive connections?";          expected = "technical" },
    @{ msg = "My integration returns success but data is not saved";               expected = "technical" },
    @{ msg = "The dependency injection container throws on startup";               expected = "technical" },
    @{ msg = "How do I configure circuit breakers for downstream services?";       expected = "technical" },
    @{ msg = "I'm seeing high p99 latency spikes every hour";                      expected = "technical" },
    @{ msg = "How do I enable verbose debug output in the SDK?";                   expected = "technical" },
    @{ msg = "Getting 'invalid JSON' error parsing a response I cannot control";   expected = "technical" },
    @{ msg = "My Terraform apply fails on the resource creation step";             expected = "technical" },
    @{ msg = "How do I limit concurrent API calls from my application?";           expected = "technical" },
    @{ msg = "The endpoint returns paginated results but the total count is wrong"; expected = "technical" },
    @{ msg = "My service discovery is not resolving the correct host";             expected = "technical" },
    @{ msg = "How do I flush the DNS cache after a DNS change?";                   expected = "technical" },
    @{ msg = "Getting unexpected 301 redirects on POST requests";                  expected = "technical" },

    # ════════════════════════════════════════════════════════════════════════
    # SALES  (125)
    # ════════════════════════════════════════════════════════════════════════
    @{ msg = "What enterprise plans do you offer?";                                expected = "sales" },
    @{ msg = "I want to schedule a product demo";                                  expected = "sales" },
    @{ msg = "How does pricing work for large teams?";                             expected = "sales" },
    @{ msg = "Can I get a custom enterprise contract?";                            expected = "sales" },
    @{ msg = "We need licenses for 500 users";                                     expected = "sales" },
    @{ msg = "What features are included in the Pro plan?";                        expected = "sales" },
    @{ msg = "How does the free trial work?";                                      expected = "sales" },
    @{ msg = "Can we negotiate a volume discount?";                                expected = "sales" },
    @{ msg = "I want to compare your pricing plans";                               expected = "sales" },
    @{ msg = "Do you have startup pricing programs?";                              expected = "sales" },
    @{ msg = "Can you extend our trial for another month?";                        expected = "sales" },
    @{ msg = "What's included in the enterprise tier?";                            expected = "sales" },
    @{ msg = "Do you offer SLA guarantees in contracts?";                          expected = "sales" },
    @{ msg = "We want to run a proof of concept first";                            expected = "sales" },
    @{ msg = "Can we get a reseller or partner agreement?";                        expected = "sales" },
    @{ msg = "What are the differences between your plans?";                       expected = "sales" },
    @{ msg = "How do I upgrade to the Professional tier?";                         expected = "sales" },
    @{ msg = "Do you offer non-profit or NGO pricing?";                            expected = "sales" },
    @{ msg = "We need an on-premise deployment option";                            expected = "sales" },
    @{ msg = "Can I get a quote for 100 seats?";                                   expected = "sales" },
    @{ msg = "We want to run a 30-day pilot program";                              expected = "sales" },
    @{ msg = "What's the process to purchase enterprise?";                         expected = "sales" },
    @{ msg = "Do you offer academic institution discounts?";                       expected = "sales" },
    @{ msg = "I want to request a product walkthrough meeting";                    expected = "sales" },
    @{ msg = "We are comparing you against your competitors";                      expected = "sales" },
    @{ msg = "How many users can I add to the Business plan?";                     expected = "sales" },
    @{ msg = "Do you have a government or public sector pricing?";                 expected = "sales" },
    @{ msg = "I want to upgrade from Starter to Business plan";                    expected = "sales" },
    @{ msg = "Can we get a dedicated account manager?";                            expected = "sales" },
    @{ msg = "What is the minimum contract length for enterprise?";                expected = "sales" },
    @{ msg = "Do you offer white-labelling options?";                              expected = "sales" },
    @{ msg = "Can I get a custom pricing quote for our team?";                     expected = "sales" },
    @{ msg = "We'd like to see a live demo of the enterprise features";            expected = "sales" },
    @{ msg = "What's the difference between the Basic and Pro plans?";             expected = "sales" },
    @{ msg = "We need single sign-on and SAML support; which plan includes that?"; expected = "sales" },
    @{ msg = "I want to switch to the annual plan and save money";                 expected = "sales" },
    @{ msg = "Can you provide references from similar companies?";                 expected = "sales" },
    @{ msg = "We are evaluating multiple vendors; what makes you stand out?";      expected = "sales" },
    @{ msg = "Do you offer a sandbox environment for evaluation?";                 expected = "sales" },
    @{ msg = "What is the price per seat for 1000 users?";                         expected = "sales" },
    @{ msg = "Can I purchase additional storage as an add-on?";                    expected = "sales" },
    @{ msg = "Do you have flexible payment terms for large contracts?";            expected = "sales" },
    @{ msg = "We need a data processing agreement before we can proceed";          expected = "sales" },
    @{ msg = "I'd like a call with your sales team";                               expected = "sales" },
    @{ msg = "Can you provide a security and compliance overview?";                expected = "sales" },
    @{ msg = "We need the self-hosted version for our air-gapped network";         expected = "sales" },
    @{ msg = "What is your pricing model for API usage at scale?";                 expected = "sales" },
    @{ msg = "Can I get a trial of the enterprise plan before committing?";        expected = "sales" },
    @{ msg = "We need custom data retention policies; which plan supports this?";  expected = "sales" },
    @{ msg = "What professional services do you offer for onboarding?";            expected = "sales" },
    @{ msg = "I want to see how your product compares to Competitor X";            expected = "sales" },
    @{ msg = "Can we integrate your product into our existing stack?";             expected = "sales" },
    @{ msg = "Do you support multi-region data residency for compliance?";         expected = "sales" },
    @{ msg = "I need a formal proposal document for our procurement team";         expected = "sales" },
    @{ msg = "What training and onboarding is included in the enterprise plan?";   expected = "sales" },
    @{ msg = "Can I get a discounted rate for a 3-year commitment?";               expected = "sales" },
    @{ msg = "We want to run a 90-day proof of value before signing";              expected = "sales" },
    @{ msg = "Do you offer custom SLA uptime guarantees above 99.9%?";            expected = "sales" },
    @{ msg = "I need a signed NDA before discussing pricing details";              expected = "sales" },
    @{ msg = "What is the process for a large-scale rollout across 50 offices?";   expected = "sales" },
    @{ msg = "Can we co-brand the product with our company logo?";                 expected = "sales" },
    @{ msg = "We need HIPAA compliance; do you support that?";                     expected = "sales" },
    @{ msg = "How long does enterprise onboarding typically take?";                expected = "sales" },
    @{ msg = "I want to become a certified reseller of your product";              expected = "sales" },
    @{ msg = "Can you customise the feature set for our specific industry?";       expected = "sales" },
    @{ msg = "What integrations come out of the box with the Enterprise plan?";    expected = "sales" },
    @{ msg = "I'd like to discuss a strategic partnership";                        expected = "sales" },
    @{ msg = "We need dedicated cloud infrastructure, not shared";                 expected = "sales" },
    @{ msg = "Can we have a trial with our real data before purchasing?";          expected = "sales" },
    @{ msg = "What is included in the premium support tier?";                      expected = "sales" },
    @{ msg = "I want to move from the free plan to paid; what are my options?";    expected = "sales" },
    @{ msg = "Do you offer a nonprofit discount for registered charities?";        expected = "sales" },
    @{ msg = "Can you provide a ROI analysis for the enterprise plan?";            expected = "sales" },
    @{ msg = "We need 24/7 dedicated support with a named engineer";               expected = "sales" },
    @{ msg = "What features are on the product roadmap for next year?";            expected = "sales" },
    @{ msg = "I'm the CFO and want to discuss the commercial terms";               expected = "sales" },
    @{ msg = "Can you provide references from Fortune 500 customers?";             expected = "sales" },
    @{ msg = "We want to be a design partner and influence the roadmap";           expected = "sales" },
    @{ msg = "Do you have a marketplace or app exchange?";                         expected = "sales" },
    @{ msg = "Can I negotiate the overage pricing in our contract?";               expected = "sales" },
    @{ msg = "What is the process to add more seats mid-contract?";                expected = "sales" },
    @{ msg = "We need a multi-tenant architecture with data isolation";             expected = "sales" },
    @{ msg = "I want to explore the partner channel programme";                    expected = "sales" },
    @{ msg = "Can the enterprise plan include custom reporting dashboards?";        expected = "sales" },
    @{ msg = "We are doing a vendor risk assessment; can you fill in our questionnaire?"; expected = "sales" },
    @{ msg = "I need a quote that includes implementation and training costs";     expected = "sales" },
    @{ msg = "Do you offer a startup programme with discounted rates?";            expected = "sales" },
    @{ msg = "Can we get IP licensing for on-premise deployment?";                 expected = "sales" },
    @{ msg = "What is the price difference between monthly and annual billing?";   expected = "sales" },
    @{ msg = "I want to know the total cost of ownership for the enterprise plan"; expected = "sales" },
    @{ msg = "We are looking for a strategic vendor to partner with long term";    expected = "sales" },
    @{ msg = "Can you provide case studies from our industry vertical?";           expected = "sales" },
    @{ msg = "I'd like to understand your pricing tiers before making a decision"; expected = "sales" },
    @{ msg = "Do you have a channel partner portal for resellers?";                expected = "sales" },
    @{ msg = "Can we get a custom contract with specific data sovereignty clauses?"; expected = "sales" },
    @{ msg = "What is the onboarding timeline for a 200-person deployment?";       expected = "sales" },
    @{ msg = "I'm comparing your enterprise plan to another vendor";               expected = "sales" },
    @{ msg = "Can we have a call to discuss a multi-year deal?";                   expected = "sales" },
    @{ msg = "Do you offer discounts for paying the full year upfront?";           expected = "sales" },
    @{ msg = "We need a vendor assessment form completed for procurement";         expected = "sales" },
    @{ msg = "What is the minimum seat count for the enterprise plan?";            expected = "sales" },
    @{ msg = "I want to trial the advanced analytics features before buying";      expected = "sales" },
    @{ msg = "Can we schedule a technical deep-dive call with your team?";         expected = "sales" },
    @{ msg = "Do you provide migration assistance from our current tool?";         expected = "sales" },
    @{ msg = "I need to know your data centre locations for compliance reasons";   expected = "sales" },
    @{ msg = "Can you host a workshop for our team as part of the contract?";      expected = "sales" },
    @{ msg = "What certifications does your product have (SOC2, ISO27001)?";       expected = "sales" },
    @{ msg = "I want to discuss adding custom modules to the enterprise plan";     expected = "sales" },
    @{ msg = "Can we get early access to beta features as an enterprise customer?"; expected = "sales" },
    @{ msg = "We need a pilot with a subset of our team before full rollout";      expected = "sales" },
    @{ msg = "What is the upgrade path from SMB to enterprise?";                   expected = "sales" },
    @{ msg = "Do you offer implementation support during the first 90 days?";      expected = "sales" },
    @{ msg = "I want to get a formal tender response for our procurement process"; expected = "sales" },
    @{ msg = "Can I see the enterprise pricing page without speaking to sales?";   expected = "sales" },
    @{ msg = "We need a dedicated private cloud deployment for our company";       expected = "sales" },
    @{ msg = "What is your policy on price increases during a multi-year contract?"; expected = "sales" },
    @{ msg = "I want to understand the licensing model for site-wide deployment";  expected = "sales" },
    @{ msg = "Can you help us build a business case for the enterprise purchase?"; expected = "sales" },
    @{ msg = "We need SSO, RBAC, and audit logging; which plan covers all three?"; expected = "sales" },
    @{ msg = "I want to talk to a solutions engineer about our use case";          expected = "sales" },
    @{ msg = "Can we get a free premium tier for 60 days to evaluate?";            expected = "sales" },
    @{ msg = "What is your partner revenue share model?";                          expected = "sales" },

    # ════════════════════════════════════════════════════════════════════════
    # GENERAL  (125)
    # ════════════════════════════════════════════════════════════════════════
    @{ msg = "Hello, how do I get started?";                                       expected = "general" },
    @{ msg = "What is this product exactly?";                                      expected = "general" },
    @{ msg = "Can you help me with something?";                                    expected = "general" },
    @{ msg = "I have a general question";                                          expected = "general" },
    @{ msg = "How does the mobile app work?";                                      expected = "general" },
    @{ msg = "Where can I find the documentation?";                                expected = "general" },
    @{ msg = "Is there a status page for service outages?";                        expected = "general" },
    @{ msg = "How do I contact support?";                                          expected = "general" },
    @{ msg = "What are your business hours?";                                      expected = "general" },
    @{ msg = "Can I speak to a human agent?";                                      expected = "general" },
    @{ msg = "I want to change my account settings";                               expected = "general" },
    @{ msg = "How do I update my profile information?";                            expected = "general" },
    @{ msg = "Where do I find tutorials and guides?";                              expected = "general" },
    @{ msg = "Is there planned maintenance tonight?";                              expected = "general" },
    @{ msg = "Can you explain how this feature works?";                            expected = "general" },
    @{ msg = "I'm new here, where should I start?";                                expected = "general" },
    @{ msg = "What's new in the latest product update?";                           expected = "general" },
    @{ msg = "How do I reset my password?";                                        expected = "general" },
    @{ msg = "What languages is the product available in?";                        expected = "general" },
    @{ msg = "I need help with my account";                                        expected = "general" },
    @{ msg = "Can you walk me through the basics?";                                expected = "general" },
    @{ msg = "Where is the user guide or manual?";                                 expected = "general" },
    @{ msg = "How do I navigate the main dashboard?";                              expected = "general" },
    @{ msg = "What third-party integrations do you support?";                      expected = "general" },
    @{ msg = "Hi there!";                                                          expected = "general" },
    @{ msg = "How do I change my notification preferences?";                       expected = "general" },
    @{ msg = "Can I use the product on multiple devices?";                         expected = "general" },
    @{ msg = "How do I invite a colleague to my workspace?";                       expected = "general" },
    @{ msg = "Is there a dark mode available?";                                    expected = "general" },
    @{ msg = "How do I export my data?";                                           expected = "general" },
    @{ msg = "What file formats are supported for import?";                        expected = "general" },
    @{ msg = "How long does my data get stored?";                                  expected = "general" },
    @{ msg = "Can I use the product offline?";                                     expected = "general" },
    @{ msg = "Is the product available in Japanese?";                              expected = "general" },
    @{ msg = "How do I search for something inside the app?";                      expected = "general" },
    @{ msg = "Where can I provide product feedback?";                              expected = "general" },
    @{ msg = "How do I delete my account?";                                        expected = "general" },
    @{ msg = "What browsers are officially supported?";                            expected = "general" },
    @{ msg = "Can I customise the interface layout?";                              expected = "general" },
    @{ msg = "How do I set up two-factor authentication?";                         expected = "general" },
    @{ msg = "I forgot my username, how do I recover it?";                         expected = "general" },
    @{ msg = "Is there a community forum or user group?";                          expected = "general" },
    @{ msg = "How do I report a bug?";                                             expected = "general" },
    @{ msg = "What is the maximum file size I can upload?";                        expected = "general" },
    @{ msg = "Can I collaborate in real time with my team?";                       expected = "general" },
    @{ msg = "How do I pin a tab or favourite an item?";                           expected = "general" },
    @{ msg = "Is there a keyboard shortcut guide?";                                expected = "general" },
    @{ msg = "How do I switch between multiple accounts?";                         expected = "general" },
    @{ msg = "Can I archive old projects?";                                        expected = "general" },
    @{ msg = "How do I share a link to a specific page?";                          expected = "general" },
    @{ msg = "What is the difference between an admin and a member role?";         expected = "general" },
    @{ msg = "How do I change the default language?";                              expected = "general" },
    @{ msg = "Does the product have an iPad app?";                                 expected = "general" },
    @{ msg = "How do I transfer ownership of my account?";                         expected = "general" },
    @{ msg = "Where is the changelog or release notes?";                           expected = "general" },
    @{ msg = "How do I set my availability status?";                               expected = "general" },
    @{ msg = "Can I embed the tool in my own website?";                            expected = "general" },
    @{ msg = "How do I bulk import a list of contacts?";                           expected = "general" },
    @{ msg = "What is the storage quota for my plan?";                             expected = "general" },
    @{ msg = "Is there a desktop app for Mac?";                                    expected = "general" },
    @{ msg = "How do I enable email notifications?";                               expected = "general" },
    @{ msg = "Can I attach files to messages?";                                    expected = "general" },
    @{ msg = "How do I create a custom workspace?";                                expected = "general" },
    @{ msg = "Is the platform GDPR compliant?";                                    expected = "general" },
    @{ msg = "How do I find a specific user in the directory?";                    expected = "general" },
    @{ msg = "Can I restore a deleted item?";                                      expected = "general" },
    @{ msg = "How do I lock a document so no one else can edit it?";               expected = "general" },
    @{ msg = "What's the best way to onboard new team members?";                   expected = "general" },
    @{ msg = "How do I create a template for repeated tasks?";                     expected = "general" },
    @{ msg = "Can I link two items together?";                                     expected = "general" },
    @{ msg = "How do I set a due date reminder?";                                  expected = "general" },
    @{ msg = "Where do I manage my connected applications?";                       expected = "general" },
    @{ msg = "How do I filter results by date range?";                             expected = "general" },
    @{ msg = "Can I duplicate a workspace or project?";                            expected = "general" },
    @{ msg = "How do I leave a workspace I was invited to?";                       expected = "general" },
    @{ msg = "Is there an audit log for admin actions?";                           expected = "general" },
    @{ msg = "How do I find the version number of the app?";                       expected = "general" },
    @{ msg = "Can I use the product in China?";                                    expected = "general" },
    @{ msg = "How do I add a custom domain to my workspace?";                      expected = "general" },
    @{ msg = "Where can I watch video tutorials?";                                 expected = "general" },
    @{ msg = "How do I print a page or report?";                                   expected = "general" },
    @{ msg = "Can I sort a list by a custom field?";                               expected = "general" },
    @{ msg = "How do I merge two accounts?";                                       expected = "general" },
    @{ msg = "Is there an API for third-party developers?";                        expected = "general" },
    @{ msg = "How do I manage access permissions for a folder?";                   expected = "general" },
    @{ msg = "Can I subscribe to notifications for a specific item?";              expected = "general" },
    @{ msg = "How do I change the colour theme of my workspace?";                  expected = "general" },
    @{ msg = "What accessibility features does the product support?";              expected = "general" },
    @{ msg = "How do I recover a forgotten password?";                             expected = "general" },
    @{ msg = "Can I automate repetitive tasks without coding?";                    expected = "general" },
    @{ msg = "How do I add a profile picture?";                                    expected = "general" },
    @{ msg = "What does the red badge notification mean?";                         expected = "general" },
    @{ msg = "How do I mention someone in a comment?";                             expected = "general" },
    @{ msg = "Can I use markdown formatting in text fields?";                      expected = "general" },
    @{ msg = "How do I share my screen during a meeting?";                         expected = "general" },
    @{ msg = "Where do I find the privacy policy?";                                expected = "general" },
    @{ msg = "How do I log out from all devices?";                                 expected = "general" },
    @{ msg = "Can I sync data with Google Drive or Dropbox?";                      expected = "general" },
    @{ msg = "How do I enable or disable a feature flag?";                         expected = "general" },
    @{ msg = "What is the uptime SLA for the service?";                            expected = "general" },
    @{ msg = "How do I contact the legal or compliance team?";                     expected = "general" },
    @{ msg = "Can I give feedback on a specific feature I don't like?";            expected = "general" },
    @{ msg = "How do I add subtasks to a task?";                                   expected = "general" },
    @{ msg = "Is there a way to use the product without an internet connection?";  expected = "general" },
    @{ msg = "How do I set my time zone?";                                         expected = "general" },
    @{ msg = "Can I view the product in full-screen mode?";                        expected = "general" },
    @{ msg = "How do I create a new team within my organisation?";                 expected = "general" },
    @{ msg = "Where do I find sample data or demo content?";                       expected = "general" },
    @{ msg = "How do I copy text from a read-only field?";                         expected = "general" },
    @{ msg = "Can I use the product with a screen reader?";                        expected = "general" },
    @{ msg = "How do I reorder columns in the table view?";                        expected = "general" },
    @{ msg = "Is there a way to follow activity on another user's workspace?";     expected = "general" },
    @{ msg = "How do I close my account permanently?";                             expected = "general" },
    @{ msg = "Can I restrict access to specific IP addresses?";                    expected = "general" },
    @{ msg = "How do I turn off the in-app chat?";                                 expected = "general" },
    @{ msg = "What does the yellow warning icon mean?";                            expected = "general" },
    @{ msg = "How do I import data from a CSV file?";                              expected = "general" },
    @{ msg = "Can I post a public announcement to all members?";                   expected = "general" },
    @{ msg = "How do I see who last edited a document?";                           expected = "general" },
    @{ msg = "Is there a way to track time spent on tasks?";                       expected = "general" },
    @{ msg = "How do I change my login email address?";                            expected = "general" }
)

# ─── Run tests ────────────────────────────────────────────────────────────────
$results    = @()
$categories = @{}
$confDist   = @{ High = 0; Medium = 0; Low = 0 }

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
            Pass       = $pass
        }

        if (-not $categories.ContainsKey($t.expected)) {
            $categories[$t.expected] = @{ Pass = 0; Fail = 0 }
        }
        if ($pass) { $categories[$t.expected].Pass++ } else { $categories[$t.expected].Fail++ }

        $confDist[$r.confidence]++

        $color = if ($pass) { "Green" } else { "Red" }
        $icon  = if ($pass) { "PASS" } else { "FAIL" }
        $label = $t.expected.ToUpper().PadRight(9)
        $msg55 = $t.msg.Substring(0, [Math]::Min(55, $t.msg.Length)).PadRight(55)
        Write-Host "[$icon] [$label] $msg55 → $($r.selectedTree) (score=$($r.selectedScore.ToString('F3')), $($r.confidence))" `
            -ForegroundColor $color

    } catch {
        $errMsg = $_.Exception.Message
        $results += [PSCustomObject]@{
            Msg        = $t.msg
            Expected   = $t.expected
            Got        = "ERROR"
            Score      = 0
            Confidence = "N/A"
            Pass       = $false
        }
        if (-not $categories.ContainsKey($t.expected)) {
            $categories[$t.expected] = @{ Pass = 0; Fail = 0 }
        }
        $categories[$t.expected].Fail++
        Write-Host "[FAIL] [$($t.expected.ToUpper().PadRight(9))] $($t.msg.Substring(0, [Math]::Min(55,$t.msg.Length)).PadRight(55)) → ERROR: $errMsg" -ForegroundColor Red
    }
}

# ─── Summary ──────────────────────────────────────────────────────────────────
Write-Host ""
Write-Host ("=" * 60) -ForegroundColor DarkGray
Write-Host "RESULTS SUMMARY" -ForegroundColor Cyan
Write-Host ("=" * 60) -ForegroundColor DarkGray

$total     = $results.Count
$totalPass = ($results | Where-Object { $_.Pass }).Count
$totalFail = $total - $totalPass
$pct       = [Math]::Round(($totalPass / $total) * 100, 1)

Write-Host ""
Write-Host "Category Results:" -ForegroundColor White
foreach ($cat in @("billing", "technical", "sales", "general")) {
    if ($categories.ContainsKey($cat)) {
        $p  = $categories[$cat].Pass
        $f  = $categories[$cat].Fail
        $cp = [Math]::Round(($p / ($p + $f)) * 100, 1)
        $catColor = if ($f -eq 0) { "Green" } elseif ($cp -ge 80) { "Yellow" } else { "Red" }
        Write-Host "  $($cat.ToUpper().PadRight(10)) : $p/$($p+$f) ($cp%)" -ForegroundColor $catColor
    }
}

Write-Host ""
Write-Host "Confidence Distribution:" -ForegroundColor White
Write-Host "  High   : $($confDist.High)"   -ForegroundColor Green
Write-Host "  Medium : $($confDist.Medium)" -ForegroundColor Yellow
Write-Host "  Low    : $($confDist.Low)"    -ForegroundColor Red

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
