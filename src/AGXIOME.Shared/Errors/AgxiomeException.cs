using System.Text.Json.Serialization;

namespace AGXIOME.Shared.Errors;

/// <summary>
/// Eccezione strutturata AGXIOME. Lo stack trace è MAI esposto all'utente:
/// - `UserMessage`: mostrata all'utente (localizzata, safe)
/// - `TechnicalMessage`: loggata internamente con stack trace
/// - `ErrorCode`: codice macchina per categorizzazione
/// </summary>
public class AgxiomeException : Exception
{
    /// <summary>Codice errore strutturato.</summary>
    public AgxiomeErrorCode ErrorCode { get; }

    /// <summary>Messaggio mostrato all'utente (mai stack trace).</summary>
    public string UserMessage { get; }

    /// <summary>Messaggio tecnico per log (con stack trace separato).</summary>
    public string TechnicalMessage { get; }

    /// <summary>Severità.</summary>
    [JsonIgnore]
    public ErrorSeverity Severity { get; }

    /// <summary>Categoria (Agent, LLM, Tool, Storage, ...).</summary>
    public string Category { get; }

    /// <summary>Se true, l'operazione può essere ritentata.</summary>
    public bool IsRetryable { get; }

    /// <summary>Dettagli aggiuntivi strutturati.</summary>
    public Dictionary<string, object>? Details { get; }

    public AgxiomeException(
        AgxiomeErrorCode errorCode,
        string? technicalMessage = null,
        string? userMessage = null,
        Exception? innerException = null,
        bool isRetryable = false,
        Dictionary<string, object>? details = null)
        : base(technicalMessage ?? ErrorCatalog.Get(errorCode).TechnicalMessage, innerException)
    {
        ErrorCode = errorCode;
        var descriptor = ErrorCatalog.Get(errorCode);
        TechnicalMessage = technicalMessage ?? descriptor.TechnicalMessage;
        UserMessage = userMessage ?? descriptor.UserMessage;
        Severity = descriptor.Severity;
        Category = descriptor.Category;
        IsRetryable = isRetryable;
        Details = details;
    }

    /// <summary>Creazione rapida con soli codici.</summary>
    public static AgxiomeException FromCode(AgxiomeErrorCode code, Exception? inner = null, bool retryable = false)
        => new(code, innerException: inner, isRetryable: retryable);

    /// <summary>Costruttore per 429 rate limit.</summary>
    public static AgxiomeException RateLimited(string provider, TimeSpan retryAfter, Exception? inner = null)
        => new(AgxiomeErrorCode.LLM_RateLimited,
            $"{provider}: rate limited. Retry after {retryAfter.TotalSeconds}s",
            "Troppe richieste. Attendere prima di riprovare.",
            inner, isRetryable: true,
            details: new() { ["provider"] = provider, ["retryAfter"] = retryAfter.TotalSeconds });

    /// <summary>Costruttore per errori DB.</summary>
    public static AgxiomeException DatabaseError(string operation, string message, Exception? inner = null)
        => new(AgxiomeErrorCode.DB_QueryFailed,
            $"DB.{operation}: {message}",
            "Errore del database. Riprovo...",
            inner, isRetryable: true,
            details: new() { ["operation"] = operation });

    /// <summary>Costruttore per errori di rete.</summary>
    public static AgxiomeException NetworkError(string target, int httpStatus, Exception? inner = null)
        => new(
            httpStatus switch { 502 => AgxiomeErrorCode.NW_BadGateway, 503 => AgxiomeErrorCode.NW_ServiceUnavailable, 504 => AgxiomeErrorCode.NW_GatewayTimeout, _ => AgxiomeErrorCode.NW_Timeout },
            $"Network: {target} returned HTTP {httpStatus}",
            "Errore di comunicazione con il server.",
            inner, isRetryable: true,
            details: new() { ["target"] = target, ["httpStatus"] = httpStatus });

    /// <summary>Costruttore per errori di autenticazione.</summary>
    public static AgxiomeException AuthFailed(string provider, Exception? inner = null)
        => new(AgxiomeErrorCode.LLM_AuthenticationFailed, $"{provider}: authentication failed", "Autenticazione fallita. Verificare le credenziali.", inner);

    /// <summary>Overload di ToString() — MAI include stack trace.</summary>
    public override string ToString()
        => $"[{ErrorCatalog.GetCode(ErrorCode)}] {UserMessage}";
}
