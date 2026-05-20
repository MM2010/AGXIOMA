namespace AGXIOME.LlmWiky;

/// <summary>Estimates cost per provider/model based on token usage.</summary>
public sealed class ProviderCostTracker
{
    private static readonly Dictionary<string, (double Input, double Output)> Costs = new()
    {
        ["OpenAI-gpt-4o"]              = (2.50, 10.00),
        ["OpenAI-gpt-4o-mini"]         = (0.15, 0.60),
        ["OpenAI-o1"]                  = (15.00, 60.00),
        ["OpenAI-o3-mini"]             = (1.10, 4.40),
        ["Anthropic-claude-sonnet-4"]  = (3.00, 15.00),
        ["Anthropic-claude-3-5-sonnet"] = (3.00, 15.00),
        ["Anthropic-claude-haiku-3-5"] = (0.80, 4.00),
        ["Anthropic-claude-opus-4"]    = (15.00, 75.00),
        ["DeepSeek-deepseek-chat"]     = (0.27, 1.10),
        ["DeepSeek-deepseek-reasoner"] = (0.55, 2.19),
        ["Ollama-*"]                   = (0, 0),
        ["Local-*"]                    = (0, 0),
        ["Gemini-*"]                   = (0.10, 0.40),
    };

    /// <summary>Estimate cost in USD for a given provider/model/token counts.</summary>
    public decimal Estimate(string provider, string model, int inputTokens, int outputTokens)
    {
        var key = Costs.Keys.FirstOrDefault(k => k == $"{provider}-{model}" || k == $"{provider}-*");
        if (key == null) return 0;
        var (inputCost, outputCost) = Costs[key];
        return (decimal)(inputTokens / 1_000_000.0 * inputCost + outputTokens / 1_000_000.0 * outputCost);
    }
}
