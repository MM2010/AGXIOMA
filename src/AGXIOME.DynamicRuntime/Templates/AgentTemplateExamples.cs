namespace AGXIOME.DynamicRuntime.Templates;

/// <summary>
/// Tre esempi few-shot di agenti dinamici completi.
/// Usati nel system prompt di AgentCodeGenerator per guidare l'LLM
/// verso lo stile corretto: interfaccia ISimpleDynamicAgent + attributo [AgxiomeAgent].
/// </summary>
public static class AgentTemplateExamples
{
    /// <summary>Esempio 1 — Trasformazione testo: maiuscolo inverso.</summary>
    public const string Example1_TextTransform = """
        using System.Threading;
        using System.Threading.Tasks;
        using AGXIOME.DynamicRuntime.Attributes;
        using AGXIOME.DynamicRuntime.Interfaces;

        [AgxiomeAgent("text-reverser", "Inverte le parole di una frase e le converte in maiuscolo")]
        public sealed class TextReverser : ISimpleDynamicAgent
        {
            public string AgentId => "text-reverser";
            public string Description => "Inverte le parole di una frase e le converte in maiuscolo";

            public Task<string> RunAsync(string input, CancellationToken ct = default)
            {
                var words = input.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
                System.Array.Reverse(words);
                return Task.FromResult(string.Join(" ", words).ToUpperInvariant());
            }
        }
        """;

    /// <summary>Esempio 2 — Validazione JSON: verifica struttura minima.</summary>
    public const string Example2_JsonValidator = """
        using System.Text.Json;
        using System.Threading;
        using System.Threading.Tasks;
        using AGXIOME.DynamicRuntime.Attributes;
        using AGXIOME.DynamicRuntime.Interfaces;

        [AgxiomeAgent("json-validator", "Verifica se l'input è JSON valido e riporta il tipo del nodo radice")]
        public sealed class JsonValidator : ISimpleDynamicAgent
        {
            public string AgentId => "json-validator";
            public string Description => "Verifica se l'input è JSON valido e riporta il tipo del nodo radice";

            public Task<string> RunAsync(string input, CancellationToken ct = default)
            {
                try
                {
                    using var doc = JsonDocument.Parse(input);
                    return Task.FromResult($"Valid JSON. Root type: {doc.RootElement.ValueKind}");
                }
                catch (JsonException ex)
                {
                    return Task.FromResult($"Invalid JSON: {ex.Message}");
                }
            }
        }
        """;

    /// <summary>Esempio 3 — Classificazione: sentiment semplice a regole.</summary>
    public const string Example3_SentimentClassifier = """
        using System;
        using System.Linq;
        using System.Threading;
        using System.Threading.Tasks;
        using AGXIOME.DynamicRuntime.Attributes;
        using AGXIOME.DynamicRuntime.Interfaces;

        [AgxiomeAgent("rule-sentiment", "Classifica il sentiment di un testo con regole lessicali semplici")]
        public sealed class RuleSentiment : ISimpleDynamicAgent
        {
            public string AgentId => "rule-sentiment";
            public string Description => "Classifica il sentiment di un testo con regole lessicali semplici";

            private static readonly string[] _positive = ["buono", "ottimo", "fantastico", "eccellente", "positivo", "great", "good", "excellent"];
            private static readonly string[] _negative = ["cattivo", "pessimo", "terribile", "negativo", "bad", "terrible", "awful"];

            public Task<string> RunAsync(string input, CancellationToken ct = default)
            {
                var lower = input.ToLowerInvariant();
                var pos = _positive.Count(w => lower.Contains(w));
                var neg = _negative.Count(w => lower.Contains(w));
                var label = pos > neg ? "POSITIVE" : neg > pos ? "NEGATIVE" : "NEUTRAL";
                return Task.FromResult($"{label} (pos={pos}, neg={neg})");
            }
        }
        """;

    /// <summary>Ritorna tutti e tre gli esempi come stringa multi-blocco per il system prompt LLM.</summary>
    public static string AllExamples =>
        $"""
        === EXAMPLE 1: Text Transformation ===
        {Example1_TextTransform}

        === EXAMPLE 2: JSON Validation ===
        {Example2_JsonValidator}

        === EXAMPLE 3: Simple Classification ===
        {Example3_SentimentClassifier}
        """;
}
