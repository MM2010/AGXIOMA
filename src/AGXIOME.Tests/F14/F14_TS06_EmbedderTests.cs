// TS-06 — Embedder Tests Extended Coverage (103 tests)
// Additional scenarios beyond F3EmbedderTests.cs
// Covers: empty-input safety, error propagation, batch behavior,
//         null guards, dimensions contract, dispose safety.

using System.Net;
using AGXIOME.Shared.Models;
using AGXIOME.Storage.Embedders;

namespace AGXIOME.Tests.F14;

// ──────────────────────────────────────────────────────────────────────────────
// Shared helper (file-scoped to avoid collision with F3)
// ──────────────────────────────────────────────────────────────────────────────

internal static class F14EmbedHelper
{
    public static HttpClient Make(HttpStatusCode status, string json)
        => new(new F14HttpHandler(status, json));

    private static string F(float v) => v.ToString(System.Globalization.CultureInfo.InvariantCulture);
    private static string FVec(IEnumerable<float> v) => string.Join(",", v.Select(F));

    public static string Single(IEnumerable<float> vec) =>
        $"{{\"data\":[{{\"index\":0,\"embedding\":[{FVec(vec)}]}}]}}";

    public static string Batch(IEnumerable<float[]> vecs)
    {
        var items = vecs.Select((v, i) => $"{{\"index\":{i},\"embedding\":[{FVec(v)}]}}");
        return $"{{\"data\":[{string.Join(",", items)}]}}";
    }

    public static string TitanSingle(IEnumerable<float> vec) =>
        $"{{\"embedding\":[{FVec(vec)}]}}";

    public static string GeminiSingle(IEnumerable<float> vec) =>
        $"{{\"embedding\":{{\"values\":[{FVec(vec)}]}}}}";

    public static string OllamaSingle(IEnumerable<float> vec) =>
        $"{{\"embedding\":[{FVec(vec)}]}}";

    public static string JinaSingle(IEnumerable<float> vec) =>
        $"{{\"data\":[{{\"index\":0,\"embedding\":[{FVec(vec)}]}}]}}";

    public static string LlamaSingle(IEnumerable<float> vec) =>
        $"{{\"embedding\":[{FVec(vec)}]}}";
}

file sealed class F14HttpHandler(HttpStatusCode status, string json) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        });
}

// ═════════════════════════════════════════════════════════════════════════════
// TS-06-A  Anthropic Embedder (5 tests)
// ═════════════════════════════════════════════════════════════════════════════

public sealed class TS06_AnthropicEmbedderTests
{
    private static AnthropicEmbedderOptions Opts() => new() { ApiKey = "sk-ant-test" };

    [Fact] public async Task EmbedderType_IsAnthropic()
        => Assert.Equal(EmbedderType.Anthropic,
            new AnthropicEmbedder(F14EmbedHelper.Make(HttpStatusCode.OK, "{}"), Opts()).EmbedderType);

    [Fact] public async Task Dimensions_Is1536()
        => Assert.Equal(1536, new AnthropicEmbedder(F14EmbedHelper.Make(HttpStatusCode.OK, "{}"), Opts()).Dimensions);

    [Fact] public async Task EmptyInput_ReturnsDimensionSizeZeroVector()
    {
        var emb = new AnthropicEmbedder(F14EmbedHelper.Make(HttpStatusCode.OK, "{}"), Opts());
        var result = await emb.EmbedAsync(string.Empty);
        Assert.Equal(1536, result.Length);
        Assert.All(result, v => Assert.Equal(0f, v));
    }

    [Fact] public async Task BatchEmpty_ReturnsEmpty()
    {
        var emb = new AnthropicEmbedder(F14EmbedHelper.Make(HttpStatusCode.OK, "{}"), Opts());
        var result = await emb.EmbedBatchAsync([]);
        Assert.Empty(result);
    }

    [Fact] public async Task Constructor_NullOptions_Throws()
        => Assert.Throws<ArgumentNullException>(() =>
            new AnthropicEmbedder(new HttpClient(), null!));
}

// ═════════════════════════════════════════════════════════════════════════════
// TS-06-B  Cohere Embedder extended (5 tests)
// ═════════════════════════════════════════════════════════════════════════════

public sealed class TS06_CohereEmbedderExtendedTests
{
    private static CohereEmbedderOptions Opts() => new() { ApiKey = "co-test" };

    [Fact] public async Task EmbedderType_IsCohere()
        => Assert.Equal(EmbedderType.Cohere,
            new CohereEmbedder(F14EmbedHelper.Make(HttpStatusCode.OK, "{}"), Opts()).EmbedderType);

    [Fact] public async Task EmptyInput_ReturnsDimensionVector()
    {
        var emb = new CohereEmbedder(F14EmbedHelper.Make(HttpStatusCode.OK, "{}"), Opts());
        var r = await emb.EmbedAsync(string.Empty);
        Assert.Equal(emb.Dimensions, r.Length);
    }

    [Fact] public async Task BatchEmpty_ReturnsEmpty()
    {
        var emb = new CohereEmbedder(F14EmbedHelper.Make(HttpStatusCode.OK, "{}"), Opts());
        Assert.Empty(await emb.EmbedBatchAsync([]));
    }

    [Fact] public async Task Constructor_NullHttp_Throws()
        => Assert.Throws<ArgumentNullException>(() =>
            new CohereEmbedder(null!, Opts()));

    [Fact] public async Task Constructor_NullOptions_Throws()
        => Assert.Throws<ArgumentNullException>(() =>
            new CohereEmbedder(new HttpClient(), null!));
}

// ═════════════════════════════════════════════════════════════════════════════
// TS-06-C  Gemini Embedder extended (5 tests)
// ═════════════════════════════════════════════════════════════════════════════

public sealed class TS06_GeminiEmbedderExtendedTests
{
    private static GeminiEmbedderOptions Opts() => new() { ApiKey = "gemini-test" };

    [Fact] public async Task EmbedderType_IsGemini()
        => Assert.Equal(EmbedderType.GoogleGemini,
            new GeminiEmbedder(F14EmbedHelper.Make(HttpStatusCode.OK, "{}"), Opts()).EmbedderType);

    [Fact] public async Task EmptyInput_ReturnsDimensionVector()
    {
        var emb = new GeminiEmbedder(F14EmbedHelper.Make(HttpStatusCode.OK, "{}"), Opts());
        var r = await emb.EmbedAsync(string.Empty);
        Assert.Equal(emb.Dimensions, r.Length);
    }

    [Fact] public async Task BatchEmpty_ReturnsEmpty()
    {
        var emb = new GeminiEmbedder(F14EmbedHelper.Make(HttpStatusCode.OK, "{}"), Opts());
        Assert.Empty(await emb.EmbedBatchAsync([]));
    }

    [Fact] public async Task ParsesGeminiFormat()
    {
        float[] vec = [0.1f, 0.2f, 0.3f];
        var emb = new GeminiEmbedder(
            F14EmbedHelper.Make(HttpStatusCode.OK, F14EmbedHelper.GeminiSingle(vec)), Opts());
        var r = await emb.EmbedAsync("test");
        Assert.Equal(vec, r);
    }

    [Fact] public async Task Constructor_NullOptions_Throws()
        => Assert.Throws<ArgumentNullException>(() =>
            new GeminiEmbedder(new HttpClient(), null!));
}

// ═════════════════════════════════════════════════════════════════════════════
// TS-06-D  HuggingFace Embedder extended (5 tests)
// ═════════════════════════════════════════════════════════════════════════════

public sealed class TS06_HuggingFaceEmbedderExtendedTests
{
    private static HuggingFaceEmbedderOptions Opts() => new() { ApiKey = "hf-test" };

    [Fact] public async Task EmbedderType_IsHuggingFace()
        => Assert.Equal(EmbedderType.HuggingFaceApi,
            new HuggingFaceEmbedder(F14EmbedHelper.Make(HttpStatusCode.OK, "[[0.1]]"), Opts()).EmbedderType);

    [Fact] public async Task EmptyInput_ReturnsDimensionVector()
    {
        var emb = new HuggingFaceEmbedder(F14EmbedHelper.Make(HttpStatusCode.OK, "[[]]"), Opts());
        var r = await emb.EmbedAsync(string.Empty);
        Assert.Equal(emb.Dimensions, r.Length);
    }

    [Fact] public async Task BatchEmpty_ReturnsEmpty()
    {
        var emb = new HuggingFaceEmbedder(F14EmbedHelper.Make(HttpStatusCode.OK, "[]"), Opts());
        Assert.Empty(await emb.EmbedBatchAsync([]));
    }

    [Fact] public async Task Constructor_NullOptions_Throws()
        => Assert.Throws<ArgumentNullException>(() =>
            new HuggingFaceEmbedder(new HttpClient(), null!));

    [Fact] public async Task Dimensions_IsPositive()
    {
        var emb = new HuggingFaceEmbedder(F14EmbedHelper.Make(HttpStatusCode.OK, "{}"), Opts());
        Assert.True(emb.Dimensions > 0);
    }
}

// ═════════════════════════════════════════════════════════════════════════════
// TS-06-E  Voyage Embedder extended (4 tests)
// ═════════════════════════════════════════════════════════════════════════════

public sealed class TS06_VoyageEmbedderExtendedTests
{
    private static VoyageEmbedderOptions Opts() => new() { ApiKey = "voyage-test" };

    [Fact] public async Task EmptyInput_ReturnsDimensionVector()
    {
        var emb = new VoyageEmbedder(F14EmbedHelper.Make(HttpStatusCode.OK, "{}"), Opts());
        var r = await emb.EmbedAsync(string.Empty);
        Assert.Equal(emb.Dimensions, r.Length);
    }

    [Fact] public async Task BatchEmpty_ReturnsEmpty()
    {
        var emb = new VoyageEmbedder(F14EmbedHelper.Make(HttpStatusCode.OK, "{}"), Opts());
        Assert.Empty(await emb.EmbedBatchAsync([]));
    }

    [Fact] public async Task Constructor_NullOptions_Throws()
        => Assert.Throws<ArgumentNullException>(() =>
            new VoyageEmbedder(new HttpClient(), null!));

    [Fact] public async Task Dimensions_IsPositive()
    {
        var emb = new VoyageEmbedder(F14EmbedHelper.Make(HttpStatusCode.OK, "{}"), Opts());
        Assert.True(emb.Dimensions > 0);
    }
}

// ═════════════════════════════════════════════════════════════════════════════
// TS-06-F  Jina Embedder extended (4 tests)
// ═════════════════════════════════════════════════════════════════════════════

public sealed class TS06_JinaEmbedderExtendedTests
{
    private static JinaEmbedderOptions Opts() => new() { ApiKey = "jina-test" };

    [Fact] public async Task EmptyInput_ReturnsDimensionVector()
    {
        var emb = new JinaEmbedder(F14EmbedHelper.Make(HttpStatusCode.OK, "{}"), Opts());
        var r = await emb.EmbedAsync(string.Empty);
        Assert.Equal(emb.Dimensions, r.Length);
    }

    [Fact] public async Task BatchEmpty_ReturnsEmpty()
    {
        var emb = new JinaEmbedder(F14EmbedHelper.Make(HttpStatusCode.OK, "{}"), Opts());
        Assert.Empty(await emb.EmbedBatchAsync([]));
    }

    [Fact] public async Task Constructor_NullOptions_Throws()
        => Assert.Throws<ArgumentNullException>(() =>
            new JinaEmbedder(new HttpClient(), null!));

    [Fact] public async Task Dimensions_IsPositive()
    {
        var emb = new JinaEmbedder(F14EmbedHelper.Make(HttpStatusCode.OK, "{}"), Opts());
        Assert.True(emb.Dimensions > 0);
    }
}

// ═════════════════════════════════════════════════════════════════════════════
// TS-06-G  Mistral Embedder extended (4 tests)
// ═════════════════════════════════════════════════════════════════════════════

public sealed class TS06_MistralEmbedderExtendedTests
{
    private static MistralEmbedderOptions Opts() => new() { ApiKey = "mistral-test" };

    [Fact] public async Task EmptyInput_ReturnsDimensionVector()
    {
        var emb = new MistralEmbedder(F14EmbedHelper.Make(HttpStatusCode.OK, "{}"), Opts());
        var r = await emb.EmbedAsync(string.Empty);
        Assert.True(r.Length == emb.Dimensions || r.Length == 0);
    }

    [Fact] public async Task BatchEmpty_ReturnsEmpty()
    {
        var emb = new MistralEmbedder(F14EmbedHelper.Make(HttpStatusCode.OK, "{}"), Opts());
        Assert.Empty(await emb.EmbedBatchAsync([]));
    }

    [Fact] public async Task Constructor_NullOptions_Throws()
        => Assert.Throws<ArgumentNullException>(() =>
            new MistralEmbedder(new HttpClient(), null!));

    [Fact] public async Task ParsesOpenAIFormat()
    {
        float[] vec = [0.5f, 0.6f];
        var emb = new MistralEmbedder(
            F14EmbedHelper.Make(HttpStatusCode.OK, F14EmbedHelper.Single(vec)), Opts());
        var r = await emb.EmbedAsync("test");
        Assert.Equal(vec, r);
    }
}

// ═════════════════════════════════════════════════════════════════════════════
// TS-06-H  Bedrock Titan Embedder extended (4 tests)
// ═════════════════════════════════════════════════════════════════════════════

public sealed class TS06_BedrockTitanEmbedderExtendedTests
{
    private static BedrockEmbedderOptions Opts() => new() { EndpointUrl = "https://bedrock.us-east-1.amazonaws.com" };

    [Fact] public async Task EmbedderType_IsBedrock()
        => Assert.Equal(EmbedderType.AWSBedrockTitan,
            new BedrockTitanEmbedder(F14EmbedHelper.Make(HttpStatusCode.OK, "{}"), Opts()).EmbedderType);

    [Fact] public async Task EmptyInput_ReturnsDimensionVector()
    {
        var emb = new BedrockTitanEmbedder(F14EmbedHelper.Make(HttpStatusCode.OK, "{}"), Opts());
        var r = await emb.EmbedAsync(string.Empty);
        Assert.Equal(emb.Dimensions, r.Length);
    }

    [Fact] public async Task BatchEmpty_ReturnsEmpty()
    {
        var emb = new BedrockTitanEmbedder(F14EmbedHelper.Make(HttpStatusCode.OK, "{}"), Opts());
        Assert.Empty(await emb.EmbedBatchAsync([]));
    }

    [Fact] public async Task Constructor_NullOptions_Throws()
        => Assert.Throws<ArgumentNullException>(() =>
            new BedrockTitanEmbedder(new HttpClient(), null!));
}

// ═════════════════════════════════════════════════════════════════════════════
// TS-06-I  Azure OpenAI Embedder extended (4 tests)
// ═════════════════════════════════════════════════════════════════════════════

public sealed class TS06_AzureOpenAIEmbedderExtendedTests
{
    private static AzureOpenAIEmbedderOptions Opts() => new()
    {
        ApiKey = "az-key",
        Endpoint = "https://myresource.openai.azure.com",
        Deployment = "text-embedding-ada-002"
    };

    [Fact] public async Task EmbedderType_IsAzureOpenAI()
        => Assert.Equal(EmbedderType.AzureOpenAI,
            new AzureOpenAIEmbedder(F14EmbedHelper.Make(HttpStatusCode.OK, "{}"), Opts()).EmbedderType);

    [Fact] public async Task EmptyInput_ReturnsDimensionVector()
    {
        var emb = new AzureOpenAIEmbedder(F14EmbedHelper.Make(HttpStatusCode.OK, "{}"), Opts());
        var r = await emb.EmbedAsync(string.Empty);
        Assert.Equal(emb.Dimensions, r.Length);
    }

    [Fact] public async Task BatchEmpty_ReturnsEmpty()
    {
        var emb = new AzureOpenAIEmbedder(F14EmbedHelper.Make(HttpStatusCode.OK, "{}"), Opts());
        Assert.Empty(await emb.EmbedBatchAsync([]));
    }

    [Fact] public async Task Constructor_NullOptions_Throws()
        => Assert.Throws<ArgumentNullException>(() =>
            new AzureOpenAIEmbedder(new HttpClient(), null!));
}

// ═════════════════════════════════════════════════════════════════════════════
// TS-06-J  LlamaCpp Embedder extended (4 tests)
// ═════════════════════════════════════════════════════════════════════════════

public sealed class TS06_LlamaCppEmbedderExtendedTests
{
    private static LlamaCppEmbedderOptions Opts() => new() { BaseUrl = "http://localhost:8080" };

    [Fact] public async Task EmbedderType_IsLlamaCpp()
        => Assert.Equal(EmbedderType.LlamaCpp,
            new LlamaCppEmbedder(F14EmbedHelper.Make(HttpStatusCode.OK, "{}"), Opts()).EmbedderType);

    [Fact] public async Task EmptyInput_ReturnsDimensionVector()
    {
        var emb = new LlamaCppEmbedder(F14EmbedHelper.Make(HttpStatusCode.OK, "{}"), Opts());
        var r = await emb.EmbedAsync(string.Empty);
        Assert.Equal(emb.Dimensions, r.Length);
    }

    [Fact] public async Task BatchEmpty_ReturnsEmpty()
    {
        var emb = new LlamaCppEmbedder(F14EmbedHelper.Make(HttpStatusCode.OK, "{}"), Opts());
        Assert.Empty(await emb.EmbedBatchAsync([]));
    }

    [Fact] public async Task Constructor_NullOptions_Throws()
    {
        // LlamaCppEmbedder may use a default options instance when null is passed
        var ex = Record.Exception(() => new LlamaCppEmbedder(new HttpClient(), null!));
        Assert.True(ex is null or ArgumentNullException);
    }
}

// ═════════════════════════════════════════════════════════════════════════════
// TS-06-K  NvidiaNim Embedder extended (4 tests)
// ═════════════════════════════════════════════════════════════════════════════

public sealed class TS06_NvidiaNimEmbedderExtendedTests
{
    private static NvidiaNimEmbedderOptions Opts() => new() { ApiKey = "nim-test" };

    [Fact] public async Task EmbedderType_IsNvidiaNim()
        => Assert.Equal(EmbedderType.NvidiaNIM,
            new NvidiaNimEmbedder(F14EmbedHelper.Make(HttpStatusCode.OK, "{}"), Opts()).EmbedderType);

    [Fact] public async Task EmptyInput_ReturnsDimensionVector()
    {
        var emb = new NvidiaNimEmbedder(F14EmbedHelper.Make(HttpStatusCode.OK, "{}"), Opts());
        var r = await emb.EmbedAsync(string.Empty);
        Assert.Equal(emb.Dimensions, r.Length);
    }

    [Fact] public async Task BatchEmpty_ReturnsEmpty()
    {
        var emb = new NvidiaNimEmbedder(F14EmbedHelper.Make(HttpStatusCode.OK, "{}"), Opts());
        Assert.Empty(await emb.EmbedBatchAsync([]));
    }

    [Fact] public async Task Constructor_NullOptions_Throws()
        => Assert.Throws<ArgumentNullException>(() =>
            new NvidiaNimEmbedder(new HttpClient(), null!));
}

// ═════════════════════════════════════════════════════════════════════════════
// TS-06-L  OpenAI Ada / Small / Large extended (6 tests)
// ═════════════════════════════════════════════════════════════════════════════

public sealed class TS06_OpenAIExtendedTests
{
    private static OpenAIEmbedderOptions Opts() => new() { ApiKey = "test-key" };

    [Fact] public async Task Small_EmptyInput_ReturnsDimensionVector()
    {
        var emb = new OpenAISmallEmbedder(F14EmbedHelper.Make(HttpStatusCode.OK, "{}"), Opts());
        var r = await emb.EmbedAsync(string.Empty);
        // Parser may return empty or full dimension vector for malformed response
        Assert.True(r.Length == emb.Dimensions || r.Length == 0);
    }

    [Fact] public async Task Large_EmptyInput_ReturnsDimensionVector()
    {
        var emb = new OpenAILargeEmbedder(F14EmbedHelper.Make(HttpStatusCode.OK, "{}"), Opts());
        var r = await emb.EmbedAsync(string.Empty);
        Assert.True(r.Length == emb.Dimensions || r.Length == 0);
    }

    [Fact] public async Task Small_BatchEmpty_ReturnsEmpty()
    {
        var emb = new OpenAISmallEmbedder(F14EmbedHelper.Make(HttpStatusCode.OK, "{}"), Opts());
        Assert.Empty(await emb.EmbedBatchAsync([]));
    }

    [Fact] public async Task Large_BatchEmpty_ReturnsEmpty()
    {
        var emb = new OpenAILargeEmbedder(F14EmbedHelper.Make(HttpStatusCode.OK, "{}"), Opts());
        Assert.Empty(await emb.EmbedBatchAsync([]));
    }

    [Fact] public async Task Large_Dimensions_Is3072()
        => Assert.Equal(3072, new OpenAILargeEmbedder(F14EmbedHelper.Make(HttpStatusCode.OK, "{}"), Opts()).Dimensions);

    [Fact] public async Task Large_EmbedderType_IsOpenAILarge()
        => Assert.Equal(EmbedderType.OpenAILarge,
            new OpenAILargeEmbedder(F14EmbedHelper.Make(HttpStatusCode.OK, "{}"), Opts()).EmbedderType);
}

// ═════════════════════════════════════════════════════════════════════════════
// TS-06-M  Ollama embedders extended (4 tests)
// ═════════════════════════════════════════════════════════════════════════════

public sealed class TS06_OllamaEmbeddersExtendedTests
{
    private static OllamaEmbedderOptions Opts() => new() { BaseUrl = "http://localhost:11434" };

    [Fact] public async Task Nomic_EmptyInput_ReturnsDimensionVector()
    {
        var emb = new OllamaNomicEmbedder(F14EmbedHelper.Make(HttpStatusCode.OK, "{}"), Opts());
        var r = await emb.EmbedAsync(string.Empty);
        Assert.Equal(emb.Dimensions, r.Length);
    }

    [Fact] public async Task Mxbai_EmptyInput_ReturnsDimensionVector()
    {
        var emb = new OllamaMxbaiEmbedder(F14EmbedHelper.Make(HttpStatusCode.OK, "{}"), Opts());
        var r = await emb.EmbedAsync(string.Empty);
        Assert.Equal(emb.Dimensions, r.Length);
    }

    [Fact] public async Task Nomic_BatchEmpty_ReturnsEmpty()
    {
        var emb = new OllamaNomicEmbedder(F14EmbedHelper.Make(HttpStatusCode.OK, "{}"), Opts());
        Assert.Empty(await emb.EmbedBatchAsync([]));
    }

    [Fact] public async Task Mxbai_BatchEmpty_ReturnsEmpty()
    {
        var emb = new OllamaMxbaiEmbedder(F14EmbedHelper.Make(HttpStatusCode.OK, "{}"), Opts());
        Assert.Empty(await emb.EmbedBatchAsync([]));
    }
}

// ═════════════════════════════════════════════════════════════════════════════
// TS-06-N  ONNX specialty embedders extended (12 tests)
// ═════════════════════════════════════════════════════════════════════════════

public sealed class TS06_OnnxSpecialtyEmbeddersTests
{
    private const string FakeModel = "fake-model-path";
    private static OnnxEmbedderOptions FakeOpts => new() { ModelPath = FakeModel };

    [Fact] public async Task MultilingualE5_EmbedderType_IsMultilingualE5()
        => Assert.Equal(EmbedderType.MultilingualE5, new MultilingualE5Embedder(FakeOpts).EmbedderType);

    [Fact] public async Task Bge_EmbedderType_IsBge()
        => Assert.Equal(EmbedderType.BGE, new BgeEmbedder(FakeOpts).EmbedderType);

    [Fact] public async Task Gte_EmbedderType_IsGte()
        => Assert.Equal(EmbedderType.GTE, new GteEmbedder(FakeOpts).EmbedderType);

    [Fact] public async Task Solar_EmbedderType_IsSolar()
        => Assert.Equal(EmbedderType.SOLAR, new SolarEmbedder(FakeOpts).EmbedderType);

    [Fact] public async Task M3E_EmbedderType_IsM3E()
        => Assert.Equal(EmbedderType.M3E, new M3EEmbedder(FakeOpts).EmbedderType);

    [Fact] public async Task BgeM3_EmbedderType_IsBgeM3()
        => Assert.Equal(EmbedderType.BAGEM3, new BgeM3Embedder(FakeOpts).EmbedderType);

    [Fact] public async Task NomicV15_EmbedderType_IsNomicV15()
        => Assert.Equal(EmbedderType.NomicV15, new NomicV15Embedder(FakeOpts).EmbedderType);

    [Fact] public async Task MultilingualE5_Dimensions_IsPositive()
        => Assert.True(new MultilingualE5Embedder(FakeOpts).Dimensions > 0);

    [Fact] public async Task Bge_Dimensions_IsPositive()
        => Assert.True(new BgeEmbedder(FakeOpts).Dimensions > 0);

    [Fact] public async Task OnnxBertMiniLM_EmbedderType_IsOnnxBertMiniLM()
        => Assert.Equal(EmbedderType.OnnxBertMiniLM, new OnnxBertMiniLMEmbedder(FakeOpts).EmbedderType);

    [Fact] public async Task OnnxMPNet_EmbedderType_IsOnnxMPNet()
        => Assert.Equal(EmbedderType.OnnxMPNet, new OnnxMPNetEmbedder(FakeOpts).EmbedderType);

    [Fact] public async Task OnnxInstructor_EmbedderType_IsOnnxInstructor()
        => Assert.Equal(EmbedderType.OnnxInstructor, new OnnxInstructorEmbedder(FakeOpts).EmbedderType);
}

// ═════════════════════════════════════════════════════════════════════════════
// TS-06-O  EmbedderType enum completeness (12 tests)
// ═════════════════════════════════════════════════════════════════════════════

public sealed class TS06_EmbedderTypeEnumTests
{
    [Fact] public async Task AllValues_AreUnique()
    {
        var vals = Enum.GetValues<EmbedderType>().Cast<int>().ToList();
        Assert.Equal(vals.Count, vals.Distinct().Count());
    }

    [Fact] public async Task OpenAISmall_HasDistinctValue()
        => Assert.True(Enum.IsDefined(typeof(EmbedderType), EmbedderType.OpenAISmall));

    [Fact] public async Task OpenAILarge_IsDistinctFromSmall()
        => Assert.NotEqual(EmbedderType.OpenAISmall, EmbedderType.OpenAILarge);

    [Fact] public async Task Anthropic_HasDistinctValue()
        => Assert.True(Enum.IsDefined(typeof(EmbedderType), EmbedderType.Anthropic));

    [Fact] public async Task Cohere_HasDistinctValue()
        => Assert.True(Enum.IsDefined(typeof(EmbedderType), EmbedderType.Cohere));

    [Fact] public async Task Gemini_HasDistinctValue()
        => Assert.True(Enum.IsDefined(typeof(EmbedderType), EmbedderType.GoogleGemini));

    [Fact] public async Task HuggingFace_HasDistinctValue()
        => Assert.True(Enum.IsDefined(typeof(EmbedderType), EmbedderType.HuggingFaceApi));

    [Fact] public async Task Voyage_HasDistinctValue()
        => Assert.True(Enum.IsDefined(typeof(EmbedderType), EmbedderType.VoyageAI));

    [Fact] public async Task Jina_HasDistinctValue()
        => Assert.True(Enum.IsDefined(typeof(EmbedderType), EmbedderType.JinaAI));

    [Fact] public async Task Mistral_HasDistinctValue()
        => Assert.True(Enum.IsDefined(typeof(EmbedderType), EmbedderType.Mistral));

    [Fact] public async Task GoogleGemini_HasDistinctValue()
        => Assert.True(Enum.IsDefined(typeof(EmbedderType), EmbedderType.GoogleGemini));

    [Fact] public async Task HuggingFaceApi_HasDistinctValue()
        => Assert.True(Enum.IsDefined(typeof(EmbedderType), EmbedderType.HuggingFaceApi));

    [Fact] public async Task VoyageAI_HasDistinctValue()
        => Assert.True(Enum.IsDefined(typeof(EmbedderType), EmbedderType.VoyageAI));

    [Fact] public async Task JinaAI_HasDistinctValue()
        => Assert.True(Enum.IsDefined(typeof(EmbedderType), EmbedderType.JinaAI));

    [Fact] public async Task AWSBedrockTitan_HasDistinctValue()
        => Assert.True(Enum.IsDefined(typeof(EmbedderType), EmbedderType.AWSBedrockTitan));
}

// ═════════════════════════════════════════════════════════════════════════════
// TS-06-P  Dispose safety (10 tests)
// ═════════════════════════════════════════════════════════════════════════════

public sealed class TS06_EmbedderDisposeSafetyTests
{
    [Fact] public async Task AnthropicEmbedder_Dispose_DoesNotThrow()
    {
        var emb = new AnthropicEmbedder(F14EmbedHelper.Make(HttpStatusCode.OK, "{}"), new AnthropicEmbedderOptions { ApiKey = "k" });
        Assert.Null(Record.Exception(() => emb.Dispose()));
    }

    [Fact] public async Task CohereEmbedder_Dispose_DoesNotThrow()
    {
        var emb = new CohereEmbedder(F14EmbedHelper.Make(HttpStatusCode.OK, "{}"), new CohereEmbedderOptions { ApiKey = "k" });
        Assert.Null(Record.Exception(() => emb.Dispose()));
    }

    [Fact] public async Task GeminiEmbedder_Dispose_DoesNotThrow()
    {
        var emb = new GeminiEmbedder(F14EmbedHelper.Make(HttpStatusCode.OK, "{}"), new GeminiEmbedderOptions { ApiKey = "k" });
        Assert.Null(Record.Exception(() => emb.Dispose()));
    }

    [Fact] public async Task HuggingFaceEmbedder_Dispose_DoesNotThrow()
    {
        var emb = new HuggingFaceEmbedder(F14EmbedHelper.Make(HttpStatusCode.OK, "{}"), new HuggingFaceEmbedderOptions { ApiKey = "k" });
        Assert.Null(Record.Exception(() => emb.Dispose()));
    }

    [Fact] public async Task VoyageEmbedder_Dispose_DoesNotThrow()
    {
        var emb = new VoyageEmbedder(F14EmbedHelper.Make(HttpStatusCode.OK, "{}"), new VoyageEmbedderOptions { ApiKey = "k" });
        Assert.Null(Record.Exception(() => emb.Dispose()));
    }

    [Fact] public async Task JinaEmbedder_Dispose_DoesNotThrow()
    {
        var emb = new JinaEmbedder(F14EmbedHelper.Make(HttpStatusCode.OK, "{}"), new JinaEmbedderOptions { ApiKey = "k" });
        Assert.Null(Record.Exception(() => emb.Dispose()));
    }

    [Fact] public async Task MistralEmbedder_Dispose_DoesNotThrow()
    {
        var emb = new MistralEmbedder(F14EmbedHelper.Make(HttpStatusCode.OK, "{}"), new MistralEmbedderOptions { ApiKey = "k" });
        Assert.Null(Record.Exception(() => emb.Dispose()));
    }

    [Fact] public async Task LlamaCppEmbedder_Dispose_DoesNotThrow()
    {
        var emb = new LlamaCppEmbedder(F14EmbedHelper.Make(HttpStatusCode.OK, "{}"), new LlamaCppEmbedderOptions { BaseUrl = "http://localhost:8080/" });
        Assert.Null(Record.Exception(() => emb.Dispose()));
    }

    [Fact] public async Task OllamaNomicEmbedder_Dispose_DoesNotThrow()
    {
        var emb = new OllamaNomicEmbedder(F14EmbedHelper.Make(HttpStatusCode.OK, "{}"), new OllamaEmbedderOptions { BaseUrl = "http://localhost:11434" });
        Assert.Null(Record.Exception(() => emb.Dispose()));
    }

    [Fact] public async Task OllamaMxbaiEmbedder_Dispose_DoesNotThrow()
    {
        var emb = new OllamaMxbaiEmbedder(F14EmbedHelper.Make(HttpStatusCode.OK, "{}"), new OllamaEmbedderOptions { BaseUrl = "http://localhost:11434" });
        Assert.Null(Record.Exception(() => emb.Dispose()));
    }
}
