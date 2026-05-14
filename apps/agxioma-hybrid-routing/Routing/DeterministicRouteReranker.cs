using AgxiomaHybridRouting.AgentServices;

namespace AgxiomaHybridRouting.Routing;

/// <summary>
/// Rule-based reranker that applies deterministic score adjustments
/// to correct systematic weaknesses of pure semantic/keyword scoring.
///
/// IMPORTANT: InitialScore is in the same 0–100 scale as the config weights
/// (SemanticWeight=65, KeywordWeight=25, PriorityWeight=10). All adjustments
/// must be expressed in the same scale (e.g. +10.0 ≈ +10 points out of 100).
///
/// Rules (applied in order, effects cumulative):
///   A.  Billing signals → billing +10; if "token" also present → technical −5
///   A2. Coupon/discount → billing +25, general −15
///   B.  upgrade + plan/price/enterprise/trial/tier → sales +20, general −10
///       upgrade + error/version/install/deploy → technical +10
///   B2. Plan/tier without "upgrade" → sales +20, general −10
///   C.  Technical strong signals → technical +12
///   D.  Billing strong signals   → billing  +12
///   E.  Sales strong signals     → sales    +12
///   E2. on-premise / self-hosted → sales +25, technical −15
///   F.  Conversation-history bias for short messages → recent tree +5
///   G.  Enterprise/procurement context → sales +20, general −10
///   H.  Billing edge cases (payment methods, bank, auto-renewal) → billing +15, general −10
///   I.  Technical edge cases (env vars, DNS, file descriptors) → technical +12
///   J.  General UI/product-usage signals → general +12, sales −8
///   K.  Pagination/records false-positive guard → billing −10
///
/// IsAmbiguous is set when the post-rerank gap < AmbiguityGap config value.
/// </summary>
public sealed class DeterministicRouteReranker : IRouteReranker
{
    private readonly HybridForestSwarmConfig _config;

    public DeterministicRouteReranker(HybridForestSwarmConfig config)
        => _config = config;

    public IReadOnlyList<RouteCandidate> Rerank(
        string request,
        IReadOnlyList<RouteCandidate> candidates,
        IReadOnlyList<ConversationMessage>? history = null)
    {
        var lower = request.ToLowerInvariant();

        // Work on a mutable copy
        var list = candidates.ToList();

        // ── Rule A: Generic billing signals ──────────────────────────────────
        bool hasBillingSignal = ContainsAny(lower,
            "charged", "invoice", "payment", "refund", "paid", "receipt",
            "coupon", "discount coupon", "apply coupon", "promo code");

        if (hasBillingSignal)
        {
            Adjust(list, "billing", +10.0, "Rule-A: billing signal");
            // "token" in context usually means auth token → reduce billing confusion
            if (lower.Contains("token"))
                Adjust(list, "technical", -5.0, "Rule-A: 'token' reduces billing");
        }

        // ── Rule A2: Strong coupon/discount-code billing signals ──────────────
        bool hasCouponSignal = ContainsAny(lower,
            "coupon", "discount code", "promo code", "apply discount", "apply coupon",
            "redemption code", "voucher code", "discount coupon");

        if (hasCouponSignal)
        {
            Adjust(list, "billing", +25.0, "Rule-A2: coupon/discount-code → strong billing signal");
            Adjust(list, "general", -15.0, "Rule-A2: coupon/discount-code → reduce general");
        }

        // ── Rule B: upgrade disambiguation ────────────────────────────────────
        if (lower.Contains("upgrade"))
        {
            if (ContainsAny(lower, "plan", "price", "enterprise", "trial", "tier", "professional", "pro "))
            {
                Adjust(list, "sales", +20.0, "Rule-B: upgrade+plan/price/enterprise/trial/tier/professional → sales");
                Adjust(list, "general", -10.0, "Rule-B: upgrade+sales-signal → reduce general");
            }
            if (ContainsAny(lower, "error", "version", "install", "deploy"))
                Adjust(list, "technical", +10.0, "Rule-B: upgrade+error/version/install/deploy → technical");
        }

        // ── Rule B2: Plan/tier upgrade without "upgrade" keyword ─────────────
        bool hasPlanTierSignal = ContainsAny(lower, "professional tier", "pro tier", "enterprise tier",
            "upgrade to professional", "upgrade to pro", "switch to professional", "switch to pro",
            "move to professional", "move to enterprise");
        if (hasPlanTierSignal)
        {
            Adjust(list, "sales", +20.0, "Rule-B2: plan tier signal → sales");
            Adjust(list, "general", -10.0, "Rule-B2: plan tier signal → reduce general");
        }

        // ── Rule C: Technical strong signals ─────────────────────────────────
        bool hasTechnicalStrong = ContainsAny(lower,
            "401", "403", "500", "exception", "stack trace", "webhook",
            "timeout", "token refresh", "api key", "ssl", "cors",
            "authentication", "certificate", "null pointer", "deploy", "cors error");

        if (hasTechnicalStrong)
        {
            Adjust(list, "technical", +12.0, "Rule-C: strong technical signal");

            // Counter: two-factor authentication is a product feature question (general),
            // not a technical debugging request.
            if (ContainsAny(lower, "two-factor", "two factor", "2fa", "multi-factor", "mfa"))
            {
                Adjust(list, "technical", -22.0, "Rule-C counter: 2FA/MFA is a general product feature");
                Adjust(list, "general", +12.0, "Rule-C counter: 2FA → boost general");
            }
        }

        // ── Rule D: Billing strong signals ────────────────────────────────────
        bool hasBillingStrong = ContainsAny(lower,
            "charged twice", "invoice wrong", "refund", "receipt", "tax invoice",
            "vat", "proration", "billing error", "double charge", "billed twice",
            "wrong amount");

        if (hasBillingStrong)
            Adjust(list, "billing", +12.0, "Rule-D: strong billing signal");

        // ── Rule E: Sales strong signals ─────────────────────────────────────
        // NOTE: "demo" intentionally uses specific phrases to avoid matching
        // "demo content" / "sample data or demo" (general UI questions).
        bool hasSalesStrong = ContainsAny(lower,
            "enterprise plan", "book a demo", "schedule a demo", "request a demo",
            "product demo", "live demo", "arrange a demo", "request demo",
            "contract", "quote", "pilot",
            "sla guarantee", "sla in contract", "sla commitment", "custom sla",
            "service level agreement",
            "reseller", "pricing plan", "trial extension",
            "proof of concept", "volume discount", "partner agreement",
            "non-profit", "nonprofit", "academic", "license",
            "on-premise", "on premise", "onpremise", "self-hosted", "self hosted",
            "professional tier", "pro plan", "upgrade to pro");

        if (hasSalesStrong)
            Adjust(list, "sales", +12.0, "Rule-E: strong sales signal");

        // ── Rule E2: On-premise is a sales/procurement concern ────────────────
        bool hasOnPremise = ContainsAny(lower, "on-premise", "on premise", "onpremise", "self-hosted", "self hosted");
        if (hasOnPremise)
        {
            Adjust(list, "sales", +25.0, "Rule-E2: on-premise → strong sales signal");
            Adjust(list, "technical", -15.0, "Rule-E2: on-premise → reduce technical");
        }

        // ── Rule F: History context bias (short messages) ─────────────────────
        if (history is { Count: > 0 } && request.Split(' ').Length <= 8)
        {
            string? lastAssistantTree = null;
            for (int i = history.Count - 1; i >= 0; i--)
            {
                var msg = history[i];
                if (msg.Role == "assistant" && !string.IsNullOrWhiteSpace(msg.Content))
                {
                    // Look for tree name mentioned in the last assistant message
                    foreach (var candidate in list)
                    {
                        if (msg.Content.Contains(candidate.TreeName, StringComparison.OrdinalIgnoreCase))
                        {
                            lastAssistantTree = candidate.TreeName;
                            break;
                        }
                    }
                    break;
                }
            }

            if (lastAssistantTree is not null)
                Adjust(list, lastAssistantTree, +5.0, "Rule-F: history context bias");
        }

        // ── Rule G: Enterprise / procurement / compliance context → sales ─────
        // Catches sales questions that lack the core Rule-E vocabulary but are
        // clearly enterprise procurement / compliance / contract discussions.
        bool hasEnterpriseSalesSignal = ContainsAny(lower,
            "dedicated account manager", "account manager",
            "white-label", "white-labelling", "white label", "whitelabel",
            "data processing agreement",
            "data retention polic",
            "professional services",
            "data residency",
            "hipaa",
            "enterprise onboarding",
            "multi-tenant",
            "vendor risk",
            "migration assistance",
            "soc2", "iso27001", "iso 27001",
            "implementation support",
            "formal tender", "tender response",
            "procurement process", "procurement",
            "solutions engineer",
            "dedicated cloud", "private cloud",
            "data centre location", "data center location",
            "licensing model",
            "multi-year",
            "named engineer",
            "rollout across",
            "saml",
            "single sign-on",
            "which plan",
            "compliance overview",
            "certifications does", "what certifications",
            "dedicated support",
            "premium support tier",
            "customer references", "references from",
            "pricing model",
            "site-wide",
            "marketplace", "app exchange",
            "onboarding timeline",
            "integrate your product", "integration with our",
            "free plan to paid", "free tier to paid", "free to paid",
            "price difference between", "price comparison", "monthly vs annual", "monthly and annual billing",
            "year commitment", "3-year", "2-year", "5-year", "year deal",
            "schedule a call", "book a call with", "deep-dive call", "technical deep-dive",
            "schedule a technical");

        if (hasEnterpriseSalesSignal)
        {
            Adjust(list, "sales", +20.0, "Rule-G: enterprise/procurement signal");
            Adjust(list, "general", -10.0, "Rule-G: enterprise signal → reduce general");
            Adjust(list, "technical", -8.0, "Rule-G: enterprise signal → reduce technical");
        }

        // ── Rule H: Billing edge cases ────────────────────────────────────────
        // Payment methods, bank interactions, account balance, auto-renewal.
        bool hasBillingEdge = ContainsAny(lower,
            "bank declined", "bank flagged", "bank rejected",
            "apple pay", "google pay", "paypal", "venmo",
            "auto-renewal", "auto renewal", "autorenewal",
            "outstanding balance", "settle the balance", "overdue balance",
            "cancel the auto", "stop the renewal",
            "what i am paying", "what am i paying", "what am i being charged",
            "paying for each month", "paying per month",
            "payment method", "payment methods",
            "direct debit", "bank transfer", "wire transfer");

        if (hasBillingEdge)
        {
            Adjust(list, "billing", +15.0, "Rule-H: billing edge case");
            Adjust(list, "general", -10.0, "Rule-H: billing edge → reduce general");
        }

        // ── Rule I: Technical edge cases ──────────────────────────────────────
        // Infra/debug terms that don't contain the core Rule-C vocabulary.
        bool hasTechnicalEdge = ContainsAny(lower,
            "environment variable", "env var",
            "file descriptor",
            "address already in use",
            "structured json logging", "structured logging",
            "database migration", "db migration", "roll back", "rollback",
            "flush dns", "dns cache", "dns change",
            "sdk documentation", "sdk throws",
            "memory leak",
            "connection pooling",
            "circuit breaker",
            "distributed tracing",
            "service mesh",
            "blue/green deploy", "canary deploy",
            "container restart", "pod fails",
            "terraform",
            "idempotency key");

        if (hasTechnicalEdge)
            Adjust(list, "technical", +12.0, "Rule-I: technical edge case");

        // ── Rule J: General UI / product-usage signals ────────────────────────
        // Prevents semantic bleed of generic product questions into sales/technical.
        bool hasGeneralUISignal = ContainsAny(lower,
            "full-screen", "full screen", "fullscreen",
            "dark mode",
            "keyboard shortcut",
            "accessibility feature",
            "multiple devices", "two devices",
            "sample data", "demo content",
            "report a bug",
            "product offline", "use offline", "without an internet",
            "audit log for admin", "audit log for",
            "api for third-party", "api for developers",
            "embed the tool", "embed in my",
            "restrict access to specific ip", "ip address restriction",
            "view the product",
            "duplicate a workspace", "duplicate a project",
            "give feedback on a", "feedback on a feature",
            "uptime sla for the service",
            "two-factor authentication", "set up two-factor", "two-factor auth");

        if (hasGeneralUISignal)
        {
            Adjust(list, "general", +12.0, "Rule-J: general UI/product-usage signal");
            Adjust(list, "sales", -8.0, "Rule-J: UI signal → reduce sales");
            Adjust(list, "technical", -6.0, "Rule-J: UI signal → reduce technical");
        }

        // ── Rule K: Pagination / records false-positive guard ─────────────────
        // "duplicate records" in a pagination context looks like billing "double charge"
        // to the embedding model. Hard-rule to correct this.
        if (lower.Contains("pagination") || lower.Contains("paginated result"))
        {
            Adjust(list, "billing", -10.0, "Rule-K: pagination context → reduce billing");
            Adjust(list, "technical", +10.0, "Rule-K: pagination context → boost technical");
        }

        // ── Apply rerank adjustments then normalise to [0,1] ─────────────────
        // Compute raw adjusted scores (may temporarily exceed 1.0 before normalisation).
        foreach (var c in list)
            c.FinalScore = c.InitialScore + c.RerankAdjustment;

        // Normalise: map [min, max] linearly into [0, 1] so every score is
        // naturally bounded and the margin between candidates is preserved.
        double minS = list.Min(c => c.FinalScore);
        double maxS = list.Max(c => c.FinalScore);
        double range = maxS - minS;
        foreach (var c in list)
            c.FinalScore = range > 1e-9 ? (c.FinalScore - minS) / range : 0.5;

        list.Sort((a, b) => b.FinalScore.CompareTo(a.FinalScore));

        return list.AsReadOnly();
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static void Adjust(List<RouteCandidate> list, string treeName, double delta, string reason)
    {
        foreach (var c in list)
        {
            if (c.TreeName.Equals(treeName, StringComparison.OrdinalIgnoreCase))
            {
                c.RerankAdjustment += delta;
                c.Explanation += string.IsNullOrEmpty(c.Explanation)
                    ? reason
                    : $"; {reason}";
            }
        }
    }

    private static bool ContainsAny(string text, params string[] patterns)
        => patterns.Any(p => text.Contains(p, StringComparison.OrdinalIgnoreCase));
}
