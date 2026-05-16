namespace AGXIOME.Shared.Errors;

/// <summary>
/// Specialized AGXIOME exception for storage and vector-store boundaries.
/// Use this to translate provider-specific failures into stable AGXIOME error contracts.
/// </summary>
public sealed class AgxiomeStorageException : AgxiomeException
{
    public string Provider { get; }

    private AgxiomeStorageException(
        AgxiomeErrorCode errorCode,
        string provider,
        string technicalMessage,
        string userMessage,
        Exception? innerException = null,
        bool isRetryable = false,
        Dictionary<string, object>? details = null)
        : base(errorCode, technicalMessage, userMessage, innerException, isRetryable, details)
    {
        Provider = provider;
    }

    public static AgxiomeStorageException InvalidCollectionName(string provider, string collection, string expectedPattern)
        => new(
            AgxiomeErrorCode.DB_InvalidCollectionName,
            provider,
            $"{provider}: invalid collection name '{collection}'. Expected pattern: {expectedPattern}.",
            "Il nome della collection non e' valido.",
            details: new()
            {
                ["provider"] = provider,
                ["collection"] = collection,
                ["expectedPattern"] = expectedPattern,
            });

    public static AgxiomeStorageException InvalidRecordIdentifier(string provider, string requirement, string identifier)
        => new(
            AgxiomeErrorCode.DB_InvalidRecordIdentifier,
            provider,
            $"{provider}: invalid record identifier '{identifier}'. Requirement: {requirement}.",
            "L'identificatore del record non e' valido.",
            details: new()
            {
                ["provider"] = provider,
                ["identifier"] = identifier,
                ["requirement"] = requirement,
            });

    public static AgxiomeStorageException ProviderOperationFailed(string provider, string operation, string target, Exception? innerException = null, bool isRetryable = false)
        => new(
            AgxiomeErrorCode.DB_ProviderOperationFailed,
            provider,
            $"{provider}: operation '{operation}' failed for '{target}'.",
            "L'operazione sul data store non e' riuscita.",
            innerException,
            isRetryable,
            new()
            {
                ["provider"] = provider,
                ["operation"] = operation,
                ["target"] = target,
            });

    public static AgxiomeStorageException CollectionMissing(string provider, string collection)
        => new(
            AgxiomeErrorCode.DB_CollectionMissing,
            provider,
            $"{provider}: collection '{collection}' does not exist.",
            "La collection richiesta non esiste.",
            details: new()
            {
                ["provider"] = provider,
                ["collection"] = collection,
            });
}