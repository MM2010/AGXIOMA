using AGXIOME.Core.DependencyInjection;
using AgxiomaLegalEmail.Tools;

namespace AgxiomaLegalEmail.Services;

/// <summary>
/// Configures the ForestSwarm "legal-router" — routes client emails
/// to the most appropriate legal specialist based on keyword matching.
/// Trees: civile, penale, amministrativo, lavoro, fallback.
/// </summary>
public static class ForestSwarmSetup
{
    public static AgxiomeServiceBuilder AddLegalRouterSwarm(this AgxiomeServiceBuilder builder)
    {
        return builder.AddForestSwarm("legal-router", forest =>
        {
            // ── 🌳 Diritto Civile ──────────────────────────────────────────
            forest.AddTree("civile")
                .AddAgent("civilista",
                    "Sei un avvocato civilista esperto in contratti, locazioni, successioni, proprietà e responsabilità civile. " +
                    "Rispondi alle richieste dei clienti citando specifici articoli del Codice Civile.")
                .WithKeywords("contratto", "locazione", "successione", "proprietà", "responsabilità",
                              "condominio", "confine", "usucapione", "servitù", "danno", "illecito",
                              "contrattuale", "inadempimento", "risarcimento", "comproprietà")

                .AddAgent("family-lawyer",
                    "Sei un avvocato specializzato in diritto di famiglia. Gestisci separazioni, divorzi, affidamento figli, " +
                    "adozioni e violenza domestica. Rispondi con riferimento agli articoli del Codice Civile pertinenti.")
                .WithKeywords("divorzio", "separazione", "affidamento", "figli", "matrimonio",
                              "famiglia", "adottare", "coniuge", "mantenimento", "alimenti")

                .AddAgent("real-estate",
                    "Sei un avvocato esperto in diritto immobiliare. Gestisci compravendite, confini, immissioni, " +
                    "e problematiche condominiali. Cita articoli specifici del Codice Civile.")
                .WithKeywords("immobile", "compravendita", "muro", "confine", "immissioni",
                              "rumore", "costruzione", "edificare", "terreno", "vista");

            // ── 🌳 Diritto Penale ──────────────────────────────────────────
            forest.AddTree("penale")
                .AddAgent("penalista",
                    "Sei un avvocato penalista esperto in reati contro il patrimonio e la persona. " +
                    "Fornisci consulenza su furto, rapina, truffa, lesioni e altri reati. " +
                    "Cita specifici articoli del Codice Penale.")
                .WithKeywords("furto", "rapina", "truffa", "lesioni", "penale",
                              "reato", "denuncia", "querela", "procedimento", "indagato",
                              "imputato", "reati", "carcerazione", "pena")

                .AddAgent("white-collar",
                    "Sei un avvocato specializzato in reati societari e bancarotta. Gestisci reati finanziari, " +
                    "falso in bilancio, corruzione. Cita gli articoli pertinenti del Codice Penale.")
                .WithKeywords("societario", "bancarotta", "falso", "bilancio", "corruzione",
                              "finanziario", "società", "fallimento", "amministratore")

                .AddAgent("criminal-defense",
                    "Sei un avvocato specializzato in difesa penale. Gestisci misure cautelari, " +
                    "impugnazioni e strategie difensive. Cita articoli del Codice Penale e di Procedura Penale.")
                .WithKeywords("difesa", "cautelare", "impugnazione", "appello", "ricorso",
                              "cassazione", "custodia", "arresto", "libertà");

            // ── 🌳 Diritto Amministrativo ──────────────────────────────────
            forest.AddTree("amministrativo")
                .AddAgent("admin-lawyer",
                    "Sei un avvocato amministrativista esperto in appalti, concessioni, edilizia e urbanistica. " +
                    "Fornisci consulenza su procedimenti amministrativi e ricorsi.")
                .WithKeywords("appalto", "concessione", "edilizia", "urbanistica", "permesso",
                              "autorizzazione", "comune", "pubblica amministrazione", "concorso",
                              "bando", "gara")

                .AddAgent("tax-lawyer",
                    "Sei un avvocato tributarista esperto in diritto tributario. Gestisci cartelle esattoriali, " +
                    "contenzioso tributario e fiscalità.")
                .WithKeywords("tasse", "tributi", "cartella", "esattoriale", "fisco",
                              "agenzia entrate", "iva", "irpef", "imu", "tributario");

            // ── 🌳 Diritto del Lavoro ──────────────────────────────────────
            forest.AddTree("lavoro")
                .AddAgent("labor-lawyer",
                    "Sei un avvocato giuslavorista esperto in diritto del lavoro. Gestisci licenziamenti, " +
                    "contratti di lavoro, infortuni e mobbing.")
                .WithKeywords("licenziamento", "lavoro", "contratto", "stipendio", "dimissioni",
                              "infortunio", "mobbing", "orario", "straordinario", "tfr",
                              "assicurazione", "indennità")

                .AddAgent("union-lawyer",
                    "Sei un avvocato esperto in diritto sindacale. Gestisci vertenze collettive, " +
                    "scioperi e contrattazione sindacale.")
                .WithKeywords("sindacato", "sciopero", "vertenza", "collettivo", "cig",
                              "contrattazione", "cassa integrazione");

            // ── 🌳 Fallback ────────────────────────────────────────────────
            forest.AddTree("fallback")
                .AddAgent("generalista",
                    "Sei un avvocato generalista. Fornisci consulenza legale di base su qualsiasi " +
                    "ambito del diritto italiano. Se il caso richiede una specializzazione, " +
                    "indica quale specialista sarebbe più appropriato.")
                .WithKeywords("consulenza", "legale", "avvocato", "diritto", "aiuto",
                              "info", "informazione", "chiarimento", "dubbio");
        });
    }
}
