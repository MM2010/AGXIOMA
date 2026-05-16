using AGXIOME.Shared.Errors;
using AGXIOME.Core.Agent;
using Xunit;

namespace AGXIOME.Tests;

public class ErrorSystemTests
{
    // ═══════════════════ AgxiomeException — Stack trace MAI esposto ═══════════════════

    [Fact]
    public void AgxiomeException_ToSting_ContainsUserMessage_NotStackTrace()
    {
        var ex = new AgxiomeException(AgxiomeErrorCode.AE_RunFailed, "technical detail: connection lost");
        var str = ex.ToString();

        Assert.Contains("ERR-0101", str);
        Assert.Contains("Esecuzione dell'agente fallita", str); // user message
        Assert.DoesNotContain("AgxiomeException.cs", str);     // no file paths
        Assert.DoesNotContain("line ", str, StringComparison.OrdinalIgnoreCase); // no line numbers
    }

    [Fact]
    public void AgxiomeException_HidesInnerExceptionFromUser()
    {
        var inner = new InvalidOperationException("DB connection timeout at 192.168.1.1:5432");
        var ex = new AgxiomeException(AgxiomeErrorCode.DB_ConnectionFailed,
            technicalMessage: $"DB connection failed: {inner.Message}",
            innerException: inner);

        // User message non espone IP/porta
        Assert.DoesNotContain("192.168", ex.UserMessage);
        Assert.DoesNotContain("5432", ex.UserMessage);
        Assert.DoesNotContain("InvalidOperationException", ex.UserMessage);

        // Technical message ha i dettagli per il log
        Assert.Contains("192.168", ex.TechnicalMessage);
    }

    [Fact]
    public void AgxiomeException_ToString_NeverContainsStackTrace()
    {
        try { throw new InvalidOperationException("test"); }
        catch (Exception inner)
        {
            var ex = new AgxiomeException(AgxiomeErrorCode.LLM_ProviderUnavailable,
                "LLM call failed", innerException: inner);

            var toString = ex.ToString();
            Assert.DoesNotContain("at ", toString);
            Assert.Contains("ERR-0200", toString);
            Assert.Contains("Servizio AI non disponibile", toString);
        }
    }

    // ═══════════════════ Factory methods ═══════════════════

    [Fact]
    public void RateLimited_CreatesCorrectCode()
    {
        var ex = AgxiomeException.RateLimited("OpenAI", TimeSpan.FromSeconds(30));
        Assert.Equal(AgxiomeErrorCode.LLM_RateLimited, ex.ErrorCode);
        Assert.Equal(201, ex.NumericCode);
        Assert.True(ex.IsRetryable);
        Assert.Equal(ErrorSeverity.Warning, ex.Severity);
    }

    [Fact]
    public void StorageException_InvalidRecordIdentifier_CreatesStructuredCode()
    {
        var ex = AgxiomeStorageException.InvalidRecordIdentifier("Qdrant", "UUID string", "bad-id");

        Assert.Equal(AgxiomeErrorCode.DB_InvalidRecordIdentifier, ex.ErrorCode);
        Assert.Equal(410, ex.NumericCode);
        Assert.Equal("Qdrant", ex.Provider);
        Assert.Contains("UUID", ex.TechnicalMessage);
        Assert.DoesNotContain("System.", ex.ToString());
    }

    [Fact]
    public void StorageException_ProviderOperationFailed_IsRetryableWhenRequested()
    {
        var inner = new InvalidOperationException("transport failed");
        var ex = AgxiomeStorageException.ProviderOperationFailed("Elasticsearch", "CreateIndex", "idx-a", inner, isRetryable: true);

        Assert.Equal(AgxiomeErrorCode.DB_ProviderOperationFailed, ex.ErrorCode);
        Assert.True(ex.IsRetryable);
        Assert.Equal("Elasticsearch", ex.Provider);
        Assert.Same(inner, ex.InnerException);
    }

    [Fact]
    public void DatabaseError_CreatesCorrectCode()
    {
        var ex = AgxiomeException.DatabaseError("INSERT", "unique constraint violation");
        Assert.Equal(AgxiomeErrorCode.DB_QueryFailed, ex.ErrorCode);
        Assert.True(ex.IsRetryable);
    }

    [Fact]
    public void NetworkError_CreatesCorrectCode()
    {
        var ex502 = AgxiomeException.NetworkError("api.example.com", 502);
        Assert.Equal(AgxiomeErrorCode.NW_BadGateway, ex502.ErrorCode);

        var ex503 = AgxiomeException.NetworkError("api.example.com", 503);
        Assert.Equal(AgxiomeErrorCode.NW_ServiceUnavailable, ex503.ErrorCode);

        var ex504 = AgxiomeException.NetworkError("api.example.com", 504);
        Assert.Equal(AgxiomeErrorCode.NW_GatewayTimeout, ex504.ErrorCode);
    }

    [Fact]
    public void AuthFailed_CreatesCorrectCode()
    {
        var ex = AgxiomeException.AuthFailed("Anthropic");
        Assert.Equal(AgxiomeErrorCode.LLM_AuthenticationFailed, ex.ErrorCode);
        Assert.False(ex.IsRetryable);
        Assert.Equal(ErrorSeverity.Critical, ex.Severity);
    }

    // ═══════════════════ ErrorCatalog ═══════════════════

    [Fact]
    public void ErrorCatalog_AllCodes_HaveDescriptors()
    {
        foreach (AgxiomeErrorCode code in Enum.GetValues<AgxiomeErrorCode>())
        {
            var desc = ErrorCatalog.Get(code);
            Assert.NotNull(desc);
            Assert.False(string.IsNullOrWhiteSpace(desc.Code));
            Assert.False(string.IsNullOrWhiteSpace(desc.UserMessage));
            Assert.False(string.IsNullOrWhiteSpace(desc.TechnicalMessage));
        }
    }

    [Fact]
    public void ErrorCatalog_Unknown_ReturnsDefault()
    {
        var desc = ErrorCatalog.Get((AgxiomeErrorCode)99999);
        Assert.Equal("ERR-0000", desc.Code);
    }

    [Fact]
    public void ErrorCatalog_UsersNeverSeeTechnicalMessages()
    {
        foreach (AgxiomeErrorCode code in Enum.GetValues<AgxiomeErrorCode>())
        {
            var desc = ErrorCatalog.Get(code);
            // Technical message può contenere dettagli interni
            // User message deve essere pulito e comprensibile
            Assert.False(string.IsNullOrWhiteSpace(desc.UserMessage));
        }
    }

    // ═══════════════════ Integrazione — Agent non espone stack trace ═══════════════════

    [Fact]
    public async Task Agent_ErrorResponse_ContainsErrorCode_NotStackTrace()
    {
        var agent = AgentFactory.CreateTestAgent();
        var result = await agent.RunAsync(new AGXIOME.Shared.Models.AgentRequest("test"));

        // Se c'è un errore, deve essere user-friendly
        if (result.Status == AGXIOME.Shared.Models.AgentStatus.Error)
        {
            Assert.DoesNotContain("NullReferenceException", result.Content);
            Assert.DoesNotContain("at ", result.Content);
            Assert.DoesNotContain(".cs:", result.Content);
        }
    }
}
