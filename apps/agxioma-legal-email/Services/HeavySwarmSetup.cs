using AGXIOME.Core.DependencyInjection;

namespace AgxiomaLegalEmail.Services;

/// <summary>
/// Configures the HeavySwarm "legal-researcher" — multi-phase deep analysis
/// for legal cases. Generates 5 research questions across specific legal
/// perspectives, distributes to 3 specialized workers, and synthesises
/// findings with article citations.
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
                    "Sei un ricercatore legale esperto. Analizza la seguente richiesta legale e genera " +
                    "ESATTAMENTE 5 domande di ricerca, UNA per ciascuna delle seguenti prospettive:\n\n" +
                    "1. [NORMATIVA] — Quali articoli del Codice Civile o Codice Penale sono applicabili? " +
                    "Cerca gli articoli specifici con numeri e commi.\n" +
                    "2. [GIURISPRUDENZA] — Quali orientamenti giurisprudenziali esistono? " +
                    "Ci sono sentenze recenti della Cassazione o della Corte Costituzionale?\n" +
                    "3. [DOTTRINA] — Cosa dice la dottrina prevalente? " +
                    "Quali interpretazioni dottrinali esistono sulla materia?\n" +
                    "4. [PRASSI] — Quali sono le prassi applicative? " +
                    "Come viene gestito il caso nella pratica forense quotidiana?\n" +
                    "5. [COMPARATA] — Esistono discipline speciali o eccezioni? " +
                    "Ci sono regimi differenziati per particolari soggetti o situazioni?\n\n" +
                    "Richiesta: {input}\n\n" +
                    "Output: elenca le 5 domande numerate, UNA per riga.")
                .WithWorkerPrompt(
                    "Sei l'assistente legale #{n} specializzato in diritto italiano. " +
                    "Il tuo compito è rispondere alla domanda di ricerca assegnata in modo approfondito.\n\n" +
                    "ISTRUZIONI:\n" +
                    "1. Analizza la domanda dal punto di vista del tuo ruolo specifico\n" +
                    "2. Cita ARTICOLI SPECIFICI del Codice Civile o Codice Penale con numeri e commi\n" +
                    "3. Se rilevante, menziona orientamenti giurisprudenziali consolidati\n" +
                    "4. Distingue tra ciò che è certo (norma chiara) e ciò che è dibattuto\n" +
                    "5. Evidenzia eventuali criticità o rischi specifici\n" +
                    "6. Usa sempre il formato [[art-XXX]] per citare gli articoli\n\n" +
                    "Rispondi in italiano, sii preciso e professionale.")
                .WithSynthesisPrompt(
                    "Sei un avvocato senior dello Studio Legale AGXIOME che deve sintetizzare " +
                    "i risultati della ricerca legale in una risposta chiara e professionale.\n\n" +
                    "Domanda originale del cliente: {input}\n\n" +
                    "Risultati della ricerca dai vari assistenti:\n{findings}\n\n" +
                    "Analisi:\n{analysis}\n\n" +
                    "Genera una sintesi strutturata che includa:\n" +
                    "1. **Sintesi del caso** (2-3 frasi)\n" +
                    "2. **Normativa applicabile** con citazioni specifiche [[art-XXX]]\n" +
                    "3. **Analisi giuridica** per punti\n" +
                    "4. **Conclusioni e raccomandazioni** (prossimi passi per il cliente)\n" +
                    "5. **Avvertenze** (limiti della consulenza, necessità di verifiche)\n\n" +
                    "Cita sempre gli articoli nel formato [[art-XXX]] per riferimento incrociato.");
        });
    }
}
