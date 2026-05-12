using AGXIOME.Shared.Models;

namespace AGXIOME.Core.LLM;

// ─────────────────────────────────────────────────────────────────────────────
// TokenCounter — CE-07
// Per-provider token counting using a BPE-approximate algorithm keyed by
// TokenizerAlgorithm enum. No external tokenizer library is required at this
// tier — character-based approximation is used for providers where the exact
// algorithm is not public. Replace with ML.Tokenizers-backed impl for Cl100k/O200k.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Result of a token count operation with metadata.</summary>
public record TokenCountResult(
    int InputTokens,
    int EstimatedOutputTokens,
    TokenizerAlgorithm Algorithm,
    bool IsApproximate
);

/// <summary>Per-provider token counter. Enum-driven algorithm selection — no magic strings.</summary>
public sealed class TokenCounter
{
    // Maps LLMProviderType → canonical tokenizer algorithm.
    private static readonly IReadOnlyDictionary<LLMProviderType, TokenizerAlgorithm> _providerMap =
        new Dictionary<LLMProviderType, TokenizerAlgorithm>
        {
            [LLMProviderType.OpenAI]       = TokenizerAlgorithm.O200kBase,   // gpt-4o / o1
            [LLMProviderType.AzureOpenAI]  = TokenizerAlgorithm.O200kBase,
            [LLMProviderType.Anthropic]    = TokenizerAlgorithm.Cl100kBase,   // approx — Claude uses its own
            [LLMProviderType.Gemini]       = TokenizerAlgorithm.Gemini,
            [LLMProviderType.DeepSeek]     = TokenizerAlgorithm.Cl100kBase,
            [LLMProviderType.Mistral]      = TokenizerAlgorithm.Mistral,
            [LLMProviderType.Ollama]       = TokenizerAlgorithm.Llama3,
            [LLMProviderType.LlamaCpp]     = TokenizerAlgorithm.Llama3,
            [LLMProviderType.LMStudio]     = TokenizerAlgorithm.Llama3,
            [LLMProviderType.GPT4All]      = TokenizerAlgorithm.Llama2,
            [LLMProviderType.Groq]         = TokenizerAlgorithm.Llama3,
            [LLMProviderType.TogetherAI]       = TokenizerAlgorithm.Llama3,
            [LLMProviderType.FireworksAI]       = TokenizerAlgorithm.Llama3,
            [LLMProviderType.HuggingFaceInference] = TokenizerAlgorithm.Llama3,
            [LLMProviderType.Local]        = TokenizerAlgorithm.Generic,
            [LLMProviderType.Custom]       = TokenizerAlgorithm.Generic,
        };

    // Average chars-per-token constants per algorithm (empirically derived from public benchmarks).
    private static readonly IReadOnlyDictionary<TokenizerAlgorithm, double> _charsPerToken =
        new Dictionary<TokenizerAlgorithm, double>
        {
            [TokenizerAlgorithm.Cl100kBase] = 4.0,
            [TokenizerAlgorithm.O200kBase]  = 3.9,
            [TokenizerAlgorithm.Llama3]     = 3.8,
            [TokenizerAlgorithm.Llama2]     = 3.6,
            [TokenizerAlgorithm.Mistral]    = 3.7,
            [TokenizerAlgorithm.Gemini]     = 4.1,
            [TokenizerAlgorithm.Generic]    = 4.0,
        };

    /// <summary>Count tokens in <paramref name="text"/> using the canonical algorithm for <paramref name="provider"/>.
    /// Always returns an approximate count — exact BPE encoding is not performed here.</summary>
    public TokenCountResult Count(string text, LLMProviderType provider)
    {
        if (string.IsNullOrEmpty(text))
            return new TokenCountResult(0, 0, TokenizerAlgorithm.Generic, true);

        var algorithm = _providerMap.TryGetValue(provider, out var alg)
            ? alg
            : TokenizerAlgorithm.Generic;

        var cpt   = _charsPerToken[algorithm];
        var input = (int)Math.Ceiling(text.Length / cpt);

        return new TokenCountResult(input, 0, algorithm, IsApproximate: true);
    }

    /// <summary>Count with explicit algorithm override. Useful for custom/local providers.</summary>
    public TokenCountResult CountWithAlgorithm(string text, TokenizerAlgorithm algorithm)
    {
        if (string.IsNullOrEmpty(text))
            return new TokenCountResult(0, 0, algorithm, true);

        var cpt   = _charsPerToken.TryGetValue(algorithm, out var c) ? c : 4.0;
        var input = (int)Math.Ceiling(text.Length / cpt);
        return new TokenCountResult(input, 0, algorithm, true);
    }

    /// <summary>Estimate total tokens for a prompt + expected completion ratio.
    /// <paramref name="completionRatio"/>: expected output length as a fraction of input (default 0.5).</summary>
    public TokenCountResult CountWithCompletion(
        string text,
        LLMProviderType provider,
        double completionRatio = 0.5)
    {
        var base_ = Count(text, provider);
        var output = (int)Math.Ceiling(base_.InputTokens * completionRatio);
        return base_ with { EstimatedOutputTokens = output };
    }

    /// <summary>Which algorithm will be used for a given provider.</summary>
    public TokenizerAlgorithm AlgorithmFor(LLMProviderType provider) =>
        _providerMap.TryGetValue(provider, out var alg) ? alg : TokenizerAlgorithm.Generic;
}
