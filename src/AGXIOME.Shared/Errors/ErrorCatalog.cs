using System.Collections.Frozen;

namespace AGXIOME.Shared.Errors;

/// <summary>
/// Catalogo completo di tutti gli errori AGXIOME.
/// Ogni codice ha: messaggio tecnico, messaggio utente, categoria, gravità.
/// Stack trace mai esposto all'utente — solo loggato internamente.
/// </summary>
public static class ErrorCatalog
{
    private static readonly FrozenDictionary<AgxiomeErrorCode, ErrorDescriptor> _errors =
        new Dictionary<AgxiomeErrorCode, ErrorDescriptor>
    {
        // Generici
        [AgxiomeErrorCode.Unknown] = new("ERR-0000", "An unknown error occurred.", "Si è verificato un errore imprevisto.", ErrorSeverity.Error, "Generic"),
        [AgxiomeErrorCode.InternalError] = new("ERR-0001", "Internal framework error.", "Errore interno del sistema.", ErrorSeverity.Critical, "Generic"),
        [AgxiomeErrorCode.ConfigurationMissing] = new("ERR-0002", "Required configuration is missing.", "Configurazione richiesta non trovata.", ErrorSeverity.Critical, "Config"),
        [AgxiomeErrorCode.OperationCancelled] = new("ERR-0006", "Operation was cancelled.", "Operazione annullata.", ErrorSeverity.Warning, "Generic"),

        // Agent
        [AgxiomeErrorCode.AE_InitFailed] = new("ERR-0100", "Agent initialization failed.", "Impossibile inizializzare l'agente.", ErrorSeverity.Critical, "Agent"),
        [AgxiomeErrorCode.AE_RunFailed] = new("ERR-0101", "Agent run failed unexpectedly.", "Esecuzione dell'agente fallita.", ErrorSeverity.Error, "Agent"),
        [AgxiomeErrorCode.AE_MaxIterationsReached] = new("ERR-0103", "Agent reached maximum iterations without resolution.", "L'agente ha raggiunto il numero massimo di tentativi.", ErrorSeverity.Warning, "Agent"),
        [AgxiomeErrorCode.AE_EngineNotConfigured] = new("ERR-0107", "No LLM engine configured. Call AgentFactory.Configure() first.", "Motore LLM non configurato.", ErrorSeverity.Critical, "Agent"),
        [AgxiomeErrorCode.AE_Disposed] = new("ERR-0108", "Agent has been disposed.", "L'agente è stato rilasciato.", ErrorSeverity.Error, "Agent"),

        // LLM
        [AgxiomeErrorCode.LLM_ProviderUnavailable] = new("ERR-0200", "LLM provider is unavailable or not responding.", "Servizio AI non disponibile.", ErrorSeverity.Error, "LLM"),
        [AgxiomeErrorCode.LLM_RateLimited] = new("ERR-0201", "LLM rate limit exceeded. Retrying with backoff.", "Troppe richieste. Attendere prima di riprovare.", ErrorSeverity.Warning, "LLM"),
        [AgxiomeErrorCode.LLM_ServerError] = new("ERR-0202", "LLM provider returned a server error.", "Errore temporaneo del servizio AI.", ErrorSeverity.Error, "LLM"),
        [AgxiomeErrorCode.LLM_Timeout] = new("ERR-0203", "LLM request timed out.", "Richiesta al servizio AI scaduta.", ErrorSeverity.Error, "LLM"),
        [AgxiomeErrorCode.LLM_AuthenticationFailed] = new("ERR-0204", "LLM authentication failed. Check API key.", "Autenticazione al servizio AI fallita.", ErrorSeverity.Critical, "LLM"),
        [AgxiomeErrorCode.LLM_InvalidResponse] = new("ERR-0205", "LLM returned an unparseable response.", "Risposta dal servizio AI non valida.", ErrorSeverity.Error, "LLM"),
        [AgxiomeErrorCode.LLM_ContextWindowExceeded] = new("ERR-0206", "Context window exceeded. Reduce input size.", "Contesto troppo lungo per il modello AI.", ErrorSeverity.Warning, "LLM"),
        [AgxiomeErrorCode.LLM_ContentFiltered] = new("ERR-0207", "LLM response was filtered by content policy.", "Risposta filtrata dalle policy di sicurezza.", ErrorSeverity.Warning, "LLM"),
        [AgxiomeErrorCode.LLM_FallbackAllExhausted] = new("ERR-0209", "All LLM providers in fallback chain failed.", "Tutti i servizi AI non sono disponibili.", ErrorSeverity.Critical, "LLM"),

        // Tools
        [AgxiomeErrorCode.TL_NotFound] = new("ERR-0300", "Tool not found in registry.", "Strumento richiesto non trovato.", ErrorSeverity.Error, "Tool"),
        [AgxiomeErrorCode.TL_ExecutionFailed] = new("ERR-0301", "Tool execution failed.", "Esecuzione dello strumento fallita.", ErrorSeverity.Error, "Tool"),
        [AgxiomeErrorCode.TL_Timeout] = new("ERR-0302", "Tool execution timed out.", "Lo strumento ha impiegato troppo tempo.", ErrorSeverity.Error, "Tool"),
        [AgxiomeErrorCode.TL_InvalidArguments] = new("ERR-0303", "Invalid arguments passed to tool.", "Argomenti non validi per lo strumento.", ErrorSeverity.Warning, "Tool"),
        [AgxiomeErrorCode.TL_HITLRejected] = new("ERR-0306", "Tool execution was rejected by user.", "Esecuzione rifiutata dall'utente.", ErrorSeverity.Info, "Tool"),

        // DB
        [AgxiomeErrorCode.DB_ConnectionFailed] = new("ERR-0400", "Database connection failed.", "Connessione al database fallita.", ErrorSeverity.Critical, "Storage"),
        [AgxiomeErrorCode.DB_QueryFailed] = new("ERR-0401", "Database query execution failed.", "Errore durante l'esecuzione della query.", ErrorSeverity.Error, "Storage"),
        [AgxiomeErrorCode.DB_TransactionFailed] = new("ERR-0402", "Database transaction failed and was rolled back.", "Transazione fallita, modifiche annullate.", ErrorSeverity.Error, "Storage"),
        [AgxiomeErrorCode.DB_DeadlockDetected] = new("ERR-0403", "Database deadlock detected. Retrying.", "Conflitto nel database. Riprovo...", ErrorSeverity.Warning, "Storage"),
        [AgxiomeErrorCode.DB_EntityNotFound] = new("ERR-0405", "Entity not found in database.", "Elemento non trovato.", ErrorSeverity.Warning, "Storage"),
        [AgxiomeErrorCode.DB_CircuitBreakerOpen] = new("ERR-0408", "Database circuit breaker is open. Backend unavailable.", "Database non disponibile. Attendo il ripristino.", ErrorSeverity.Critical, "Storage"),
        [AgxiomeErrorCode.DB_InvalidCollectionName] = new("ERR-0409", "Collection name is invalid for the selected storage provider.", "Il nome della collection non e' valido.", ErrorSeverity.Warning, "Storage"),
        [AgxiomeErrorCode.DB_InvalidRecordIdentifier] = new("ERR-0410", "Record identifier is invalid for the selected storage provider.", "L'identificatore del record non e' valido.", ErrorSeverity.Warning, "Storage"),
        [AgxiomeErrorCode.DB_ProviderOperationFailed] = new("ERR-0411", "Storage provider operation failed.", "L'operazione sul data store non e' riuscita.", ErrorSeverity.Error, "Storage"),
        [AgxiomeErrorCode.DB_CollectionMissing] = new("ERR-0412", "Requested collection does not exist.", "La collection richiesta non esiste.", ErrorSeverity.Warning, "Storage"),

        // X-Link
        [AgxiomeErrorCode.XL_ChecksumMismatch] = new("ERR-0600", "X-Link checksum verification failed. Data may be corrupted.", "Verifica dei dati fallita. Possibile corruzione.", ErrorSeverity.Error, "XLink"),
        [AgxiomeErrorCode.XL_PayloadCorrupted] = new("ERR-0601", "X-Link payload is corrupted.", "Dati corrotti durante il trasporto.", ErrorSeverity.Error, "XLink"),
        [AgxiomeErrorCode.XL_DeliveryFailed] = new("ERR-0602", "X-Link message delivery failed.", "Consegna del messaggio fallita.", ErrorSeverity.Error, "XLink"),
        [AgxiomeErrorCode.XL_RouteNotFound] = new("ERR-0606", "X-Link route not found.", "Destinazione del messaggio non trovata.", ErrorSeverity.Warning, "XLink"),

        // RAG
        [AgxiomeErrorCode.RG_IngestFailed] = new("ERR-0700", "Knowledge ingestion failed.", "Indicizzazione del documento fallita.", ErrorSeverity.Error, "RAG"),
        [AgxiomeErrorCode.RG_SearchFailed] = new("ERR-0702", "Knowledge search failed.", "Ricerca nella knowledge base fallita.", ErrorSeverity.Error, "RAG"),
        [AgxiomeErrorCode.RG_EmbeddingFailed] = new("ERR-0701", "Embedding generation failed.", "Generazione dell'embedding fallita.", ErrorSeverity.Error, "RAG"),

        // Security
        [AgxiomeErrorCode.SE_AuthenticationFailed] = new("ERR-0800", "Authentication failed.", "Autenticazione fallita.", ErrorSeverity.Critical, "Security"),
        [AgxiomeErrorCode.SE_AuthorizationDenied] = new("ERR-0801", "Authorization denied.", "Accesso negato.", ErrorSeverity.Error, "Security"),
        [AgxiomeErrorCode.SE_SignatureVerificationFailed] = new("ERR-0803", "Signature verification failed.", "Verifica della firma fallita.", ErrorSeverity.Critical, "Security"),
        [AgxiomeErrorCode.SE_RateLimitExceeded] = new("ERR-0806", "Rate limit exceeded.", "Troppe richieste.", ErrorSeverity.Warning, "Security"),

        // Guardrails
        [AgxiomeErrorCode.GR_InputBlocked] = new("ERR-0900", "Input blocked by guardrail policy.", "Input bloccato dalle policy di sicurezza.", ErrorSeverity.Warning, "Guardrail"),
        [AgxiomeErrorCode.GR_OutputBlocked] = new("ERR-0901", "Output blocked by guardrail policy.", "Output bloccato dalle policy di sicurezza.", ErrorSeverity.Warning, "Guardrail"),
        [AgxiomeErrorCode.GR_PIIDetected] = new("ERR-0902", "PII detected in content.", "Dati sensibili rilevati nel contenuto.", ErrorSeverity.Warning, "Guardrail"),
        [AgxiomeErrorCode.GR_PromptInjectionDetected] = new("ERR-0903", "Prompt injection attempt detected.", "Tentativo di manipolazione rilevato.", ErrorSeverity.Warning, "Guardrail"),

        // HITL
        [AgxiomeErrorCode.HT_RequirementTimeout] = new("ERR-1000", "HITL requirement timed out.", "Richiesta di conferma scaduta.", ErrorSeverity.Warning, "HITL"),
        [AgxiomeErrorCode.HT_ExternalExecutionFailed] = new("ERR-1003", "External tool execution failed.", "Esecuzione esterna dello strumento fallita.", ErrorSeverity.Error, "HITL"),

        // Network
        [AgxiomeErrorCode.NW_Timeout] = new("ERR-1200", "Network request timed out.", "Richiesta di rete scaduta.", ErrorSeverity.Error, "Network"),
        [AgxiomeErrorCode.NW_BadGateway] = new("ERR-1205", "Bad gateway (502). Upstream server error.", "Errore del server intermedio.", ErrorSeverity.Error, "Network"),
        [AgxiomeErrorCode.NW_ServiceUnavailable] = new("ERR-1206", "Service unavailable (503).", "Servizio temporaneamente non disponibile.", ErrorSeverity.Error, "Network"),
        [AgxiomeErrorCode.NW_GatewayTimeout] = new("ERR-1207", "Gateway timeout (504).", "Timeout del server intermedio.", ErrorSeverity.Error, "Network"),

        // Config
        [AgxiomeErrorCode.CF_InvalidValue] = new("ERR-1300", "Invalid configuration value.", "Valore di configurazione non valido.", ErrorSeverity.Error, "Config"),
        [AgxiomeErrorCode.CF_MissingRequired] = new("ERR-1301", "Required configuration missing.", "Configurazione obbligatoria mancante.", ErrorSeverity.Critical, "Config"),
        [AgxiomeErrorCode.CF_ConnectionStringMissing] = new("ERR-1302", "Connection string not configured.", "Stringa di connessione non configurata.", ErrorSeverity.Critical, "Config"),
        [AgxiomeErrorCode.CF_ProviderNotFound] = new("ERR-1303", "Provider not found.", "Provider non trovato.", ErrorSeverity.Error, "Config"),
    }.ToFrozenDictionary();

    public static ErrorDescriptor Get(AgxiomeErrorCode code)
        => _errors.GetValueOrDefault(code, _errors[AgxiomeErrorCode.Unknown]);

    public static string GetUserMessage(AgxiomeErrorCode code) => Get(code).UserMessage;
    public static string GetCode(AgxiomeErrorCode code) => Get(code).Code;
}

/// <summary>Descriptor per ogni errore.</summary>
public record ErrorDescriptor(
    string Code,            // "ERR-0101"
    string TechnicalMessage, // log interno
    string UserMessage,      // mostrato all'utente — mai stack trace
    ErrorSeverity Severity,
    string Category
);

/// <summary>Gravità errore.</summary>
public enum ErrorSeverity
{
    Info,       // Solo log, nessuna azione
    Warning,    // Log + alert opzionale
    Error,      // Log + alert + retry automatico
    Critical    // Log + alert + operazione interrotta
}
