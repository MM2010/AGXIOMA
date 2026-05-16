namespace AGXIOME.Shared.Errors;

/// <summary>
/// Structured AGXIOME exception for toolkit/config/parameter boundaries.
/// </summary>
public sealed class AgxiomeToolkitException : AgxiomeException
{
    public string ToolkitId { get; }

    private AgxiomeToolkitException(
        AgxiomeErrorCode errorCode,
        string toolkitId,
        string technicalMessage,
        string userMessage,
        Exception? innerException = null,
        bool isRetryable = false,
        Dictionary<string, object>? details = null)
        : base(errorCode, technicalMessage, userMessage, innerException, isRetryable, details)
    {
        ToolkitId = toolkitId;
    }

    public static AgxiomeToolkitException MissingSetting(string toolkitId, string settingKey)
        => new(
            AgxiomeErrorCode.CF_MissingRequired,
            toolkitId,
            $"Toolkit '{toolkitId}' is missing required setting '{settingKey}'.",
            "Configurazione obbligatoria del toolkit mancante.",
            details: new() { ["toolkitId"] = toolkitId, ["settingKey"] = settingKey });

    public static AgxiomeToolkitException MissingParameter(string toolkitId, string parameterName)
        => new(
            AgxiomeErrorCode.TL_InvalidArguments,
            toolkitId,
            $"Toolkit '{toolkitId}' is missing required parameter '{parameterName}'.",
            "Parametri del toolkit non validi.",
            details: new() { ["toolkitId"] = toolkitId, ["parameterName"] = parameterName });

    public static AgxiomeToolkitException UnknownAction(string toolkitId, string action)
        => new(
            AgxiomeErrorCode.TL_InvalidArguments,
            toolkitId,
            $"Toolkit '{toolkitId}' received unsupported action '{action}'.",
            "Azione del toolkit non supportata.",
            details: new() { ["toolkitId"] = toolkitId, ["action"] = action });

    public static AgxiomeToolkitException DependencyUnavailable(string toolkitId, string dependencyName, Exception? innerException = null)
        => new(
            AgxiomeErrorCode.TL_ExecutionFailed,
            toolkitId,
            $"Toolkit '{toolkitId}' requires runtime dependency '{dependencyName}' which is not available.",
            "Dipendenza di esecuzione del toolkit non disponibile.",
            innerException,
            details: new() { ["toolkitId"] = toolkitId, ["dependency"] = dependencyName });

    public static AgxiomeToolkitException ExecutionFailed(string toolkitId, string action, Exception? innerException = null, bool isRetryable = false)
        => new(
            AgxiomeErrorCode.TL_ExecutionFailed,
            toolkitId,
            $"Toolkit '{toolkitId}' failed while executing action '{action}'.",
            "Esecuzione dello strumento fallita.",
            innerException,
            isRetryable,
            new() { ["toolkitId"] = toolkitId, ["action"] = action });
}