namespace AGXIOME.Shared.Errors;

/// <summary>
/// Codici errore strutturati per tutto il framework AGXIOME.
/// Pattern: {Modulo}{Categoria}{Numero}
/// Categorie: C=Config, E=Engine, T=Tool, S=Storage, N=Network, A=Auth, L=LLM, R=RAG, X=XLink, G=Guardrail, H=HITL, M=Memory
/// </summary>
public enum AgxiomeErrorCode
{
    // ── Generici (0-99) ──
    Unknown = 0,
    InternalError = 1,
    ConfigurationMissing = 2,
    InvalidOperation = 3,
    NotSupported = 4,
    NotImplemented = 5,
    OperationCancelled = 6,

    // ── Agent Engine (100-199) ──
    AE_InitFailed = 100,
    AE_RunFailed = 101,
    AE_StreamFailed = 102,
    AE_MaxIterationsReached = 103,
    AE_RecoveryLimitExceeded = 104,
    AE_StateCorrupted = 105,
    AE_ConcurrentRunBlocked = 106,
    AE_EngineNotConfigured = 107,
    AE_Disposed = 108,

    // ── LLM Provider (200-299) ──
    LLM_ProviderUnavailable = 200,
    LLM_RateLimited = 201,          // 429
    LLM_ServerError = 202,          // 500/502/503
    LLM_Timeout = 203,
    LLM_AuthenticationFailed = 204, // 401/403
    LLM_InvalidResponse = 205,
    LLM_ContextWindowExceeded = 206,
    LLM_ContentFiltered = 207,
    LLM_EmbeddingFailed = 208,
    LLM_FallbackAllExhausted = 209,

    // ── Tools (300-399) ──
    TL_NotFound = 300,
    TL_ExecutionFailed = 301,
    TL_Timeout = 302,
    TL_InvalidArguments = 303,
    TL_ParameterValidationFailed = 304,
    TL_ResultTooLarge = 305,
    TL_HITLRejected = 306,

    // ── Storage / Database (400-499) ──
    DB_ConnectionFailed = 400,
    DB_QueryFailed = 401,
    DB_TransactionFailed = 402,
    DB_DeadlockDetected = 403,
    DB_UniqueConstraintViolation = 404,
    DB_EntityNotFound = 405,
    DB_MigrationFailed = 406,
    DB_ConnectionPoolExhausted = 407,
    DB_CircuitBreakerOpen = 408,
    DB_InvalidCollectionName = 409,
    DB_InvalidRecordIdentifier = 410,
    DB_ProviderOperationFailed = 411,
    DB_CollectionMissing = 412,

    // ── Redis / Cache (500-599) ──
    CA_ConnectionFailed = 500,
    CA_MissAfterRetry = 501,
    CA_PersistenceFailed = 502,
    CA_InvalidationFailed = 503,

    // ── X-Link Transport (600-699) ──
    XL_ChecksumMismatch = 600,
    XL_PayloadCorrupted = 601,
    XL_DeliveryFailed = 602,
    XL_SerializationFailed = 603,
    XL_CompressionFailed = 604,
    XL_Timeout = 605,
    XL_RouteNotFound = 606,
    XL_DestinationUnreachable = 607,

    // ── RAG / Knowledge (700-799) ──
    RG_IngestFailed = 700,
    RG_EmbeddingFailed = 701,
    RG_SearchFailed = 702,
    RG_IndexNotFound = 703,
    RG_ChunkingFailed = 704,
    RG_VectorDimensionMismatch = 705,

    // ── Security (800-899) ──
    SE_AuthenticationFailed = 800,
    SE_AuthorizationDenied = 801,
    SE_CryptoOperationFailed = 802,
    SE_SignatureVerificationFailed = 803,
    SE_HMACMismatch = 804,
    SE_TokenExpired = 805,
    SE_RateLimitExceeded = 806,
    SE_AuditLogFailed = 807,

    // ── Guardrails (900-999) ──
    GR_InputBlocked = 900,
    GR_OutputBlocked = 901,
    GR_PIIDetected = 902,
    GR_PromptInjectionDetected = 903,
    GR_ContentPolicyViolation = 904,

    // ── HITL (1000-1099) ──
    HT_RequirementTimeout = 1000,
    HT_RequirementExpired = 1001,
    HT_InvalidResolution = 1002,
    HT_ExternalExecutionFailed = 1003,

    // ── Sessions (1100-1199) ──
    SS_CreationFailed = 1100,
    SS_PersistenceFailed = 1101,
    SS_HistoryTooLarge = 1102,
    SS_SummaryGenerationFailed = 1103,

    // ── Network / Infrastructure (1200-1299) ──
    NW_Timeout = 1200,
    NW_DnsResolutionFailed = 1201,
    NW_TlsHandshakeFailed = 1202,
    NW_ConnectionReset = 1203,
    NW_ProxyAuthenticationRequired = 1204,
    NW_BadGateway = 1205,          // 502
    NW_ServiceUnavailable = 1206,  // 503
    NW_GatewayTimeout = 1207,      // 504

    // ── Configuration (1300-1399) ──
    CF_InvalidValue = 1300,
    CF_MissingRequired = 1301,
    CF_ConnectionStringMissing = 1302,
    CF_ProviderNotFound = 1303,
    CF_ModuleDisabled = 1304,
}
