using AGXIOME.Core.DependencyInjection;

namespace AgxiomaLegalEmail.Services;

/// <summary>
/// Configures the HeavySwarm "legal-researcher" — multi-phase deep analysis
/// for legal cases. Generates research questions, distributes to workers,
/// and synthesises findings with article citations.
/// </summary>
public static class HeavySwarmSetup
{
    public static AgxiomeServiceBuilder AddLegalResearchSwarm(this AgxiomeServiceBuilder builder)
    {
        return builder.AddHeavySwarm("legal-researcher", heavy =>
        {
            heavy
                .WithWorkers(3)
                .WithParallelResearch()
                .WithQuestionPrompt(
                    "Analizza la seguente richiesta legale e genera 5 domande di ricerca specifiche per trovare " +
                    "gli articoli di legge pertinenti. Per ogni domanda indica:\n" +
                    "1. L'ambito normativo (Codice Civile / Codice Penale)\n" +
                    "2. La query di ricerca specifica per il database vettoriale\n" +
                    "3. Gli articoli di legge potenzialmente applicabili\n\n" +
                    "Richiesta: {input}")
                .WithSynthesisPrompt(
                    "Sei un avvocato senior che deve sintetizzare i risultati di una ricerca legale in una " +
                    "risposta chiara e professionale per il cliente.\n\n" +
                    "Domanda originale: {input}\n\n" +
                    "Risultati della ricerca:\n{findings}\n\n" +
                    "Analisi:\n{analysis}\n\n" +
                    "Genera una risposta strutturata che includa:\n" +
                    "1. **Sintesi del caso** (2-3 frasi)\n" +
                    "2. **Normativa applicabile** con citazioni specifiche degli articoli\n" +
                    "3. **Analisi giuridica** con riferimenti ai principi generali\n" +
                    "4. **Conclusioni e raccomandazioni** (prossimi passi per il cliente)\n" +
                    "5. **Avvertenze** (limiti della consulenza, necessità di verifiche)\n\n" +
                    "Cita sempre gli articoli nel formato [[art-XXXX]] per riferimento incrociato.");
        });
    }
}
