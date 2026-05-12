using System.Net;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using AGXIOME.Storage.Embedders;
using Xunit;

namespace AGXIOME.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// Helpers
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Builds an HttpClient whose handler returns a canned JSON payload.</summary>
file static class EmbedHttpHelper
{
    public static HttpClient Make(HttpStatusCode status, string json)
        => new HttpClient(new TestHttpMessageHandler(status, json));

        // Use invariant culture so floats are always serialised with '.' regardless of OS locale.
    private static string F(float v) => v.ToString(System.Globalization.CultureInfo.InvariantCulture);
    public static string FVec(IEnumerable<float> v) => string.Join(",", v.Select(F));

    /// <summary>Wraps a single float[] into an OpenAI-compat embeddings response.</summary>
    public static string OpenAISingle(IEnumerable<float> vec) =>
        $"{{\"data\":[{{\"index\":0,\"embedding\":[{FVec(vec)}]}}]}}";

    /// <summary>Wraps multiple float[][] into an OpenAI-compat embeddings response.</summary>
    public static string OpenAIBatch(IEnumerable<float[]> vecs)
    {
        var items = vecs.Select((v, i) =>
            $"{{\"index\":{i},\"embedding\":[{FVec(v)}]}}");
        return $"{{\"data\":[{string.Join(",", items)}]}}";
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// E-01 — OpenAI Small (5 tests)
// ─────────────────────────────────────────────────────────────────────────────

public class OpenAISmallEmbedderTests
{
    private static OpenAIEmbedderOptions Opts() => new() { ApiKey = "test-key" };

    [Fact]
    public void EmbedderType_IsOpenAISmall()
        => Assert.Equal(EmbedderType.OpenAISmall,
            new OpenAISmallEmbedder(EmbedHttpHelper.Make(HttpStatusCode.OK, "{}"), Opts()).EmbedderType);

    [Fact]
    public void Dimensions_Is1536()
        => Assert.Equal(1536, new OpenAISmallEmbedder(EmbedHttpHelper.Make(HttpStatusCode.OK, "{}"), Opts()).Dimensions);

    [Fact]
    public async Task EmbedAsync_ParsesVector()
    {
        float[] vec = Enumerable.Range(0, 4).Select(i => (float)i).ToArray();
        var client  = EmbedHttpHelper.Make(HttpStatusCode.OK, EmbedHttpHelper.OpenAISingle(vec));
        var emb     = new OpenAISmallEmbedder(client, Opts());
        var result  = await emb.EmbedAsync("hello");
        Assert.Equal(vec, result);
    }

    [Fact]
    public async Task EmbedBatchAsync_PreservesOrder()
    {
        float[][] vecs = [[1, 0], [0, 1]];
        var client     = EmbedHttpHelper.Make(HttpStatusCode.OK, EmbedHttpHelper.OpenAIBatch(vecs));
        var emb        = new OpenAISmallEmbedder(client, Opts());
        var result     = await emb.EmbedBatchAsync(["a", "b"]);
        Assert.Equal(2, result.Length);
        Assert.Equal(vecs[0], result[0]);
        Assert.Equal(vecs[1], result[1]);
    }

    [Fact]
    public void Constructor_NullOptions_Throws()
        => Assert.Throws<ArgumentNullException>(() =>
            new OpenAISmallEmbedder(new HttpClient(), null!));
}

// ─────────────────────────────────────────────────────────────────────────────
// E-02 — OpenAI Large (5 tests)
// ─────────────────────────────────────────────────────────────────────────────

public class OpenAILargeEmbedderTests
{
    private static OpenAIEmbedderOptions Opts() => new() { ApiKey = "key" };

    [Fact]
    public void EmbedderType_IsOpenAILarge()
        => Assert.Equal(EmbedderType.OpenAILarge,
            new OpenAILargeEmbedder(EmbedHttpHelper.Make(HttpStatusCode.OK, "{}"), Opts()).EmbedderType);

    [Fact]
    public void Dimensions_Is3072()
        => Assert.Equal(3072, new OpenAILargeEmbedder(EmbedHttpHelper.Make(HttpStatusCode.OK, "{}"), Opts()).Dimensions);

    [Fact]
    public async Task EmbedAsync_ParsesVector()
    {
        float[] vec = [0.1f, 0.2f, 0.3f];
        var emb     = new OpenAILargeEmbedder(
            EmbedHttpHelper.Make(HttpStatusCode.OK, EmbedHttpHelper.OpenAISingle(vec)), Opts());
        Assert.Equal(vec, await emb.EmbedAsync("text"));
    }

    [Fact]
    public async Task EmbedBatchAsync_EmptyInput_ReturnsEmpty()
    {
        var emb = new OpenAILargeEmbedder(
            EmbedHttpHelper.Make(HttpStatusCode.OK, "{\"data\":[]}"), Opts());
        var r   = await emb.EmbedBatchAsync([]);
        Assert.Empty(r);
    }

    [Fact]
    public void Constructor_NullOptions_Throws()
        => Assert.Throws<ArgumentNullException>(() =>
            new OpenAILargeEmbedder(new HttpClient(), null!));
}

// ─────────────────────────────────────────────────────────────────────────────
// E-03 — Anthropic (5 tests)
// ─────────────────────────────────────────────────────────────────────────────

public class AnthropicEmbedderTests
{
    private static AnthropicEmbedderOptions Opts(int dims = 1536) =>
        new() { ApiKey = "ant-key", Dimensions = dims };

    [Fact]
    public void EmbedderType_IsAnthropic()
        => Assert.Equal(EmbedderType.Anthropic,
            new AnthropicEmbedder(new HttpClient(), Opts()).EmbedderType);

    [Fact]
    public void Dimensions_ReflectsOptions()
        => Assert.Equal(512, new AnthropicEmbedder(new HttpClient(), Opts(512)).Dimensions);

    [Fact]
    public async Task EmbedAsync_EmptyText_ReturnsZeroVector()
    {
        var emb = new AnthropicEmbedder(new HttpClient(), Opts(4));
        var r   = await emb.EmbedAsync(string.Empty);
        Assert.Equal(4, r.Length);
        Assert.All(r, v => Assert.Equal(0f, v));
    }

    [Fact]
    public async Task EmbedAsync_ParsesVector()
    {
        float[] vec = [0.5f, 0.6f];
        string json = $"{{\"data\":[{{\"embedding\":[{EmbedHttpHelper.FVec(vec)}]}}]}}";
        var emb     = new AnthropicEmbedder(EmbedHttpHelper.Make(HttpStatusCode.OK, json), Opts(2));
        Assert.Equal(vec, await emb.EmbedAsync("hello"));
    }

    [Fact]
    public void Constructor_NullOptions_Throws()
        => Assert.Throws<ArgumentNullException>(() =>
            new AnthropicEmbedder(new HttpClient(), null!));
}

// ─────────────────────────────────────────────────────────────────────────────
// E-04 — Cohere (5 tests)
// ─────────────────────────────────────────────────────────────────────────────

public class CohereEmbedderTests
{
    private static CohereEmbedderOptions Opts() => new() { ApiKey = "co-key" };

    [Fact]
    public void EmbedderType_IsCohere()
        => Assert.Equal(EmbedderType.Cohere,
            new CohereEmbedder(new HttpClient(), Opts()).EmbedderType);

    [Fact]
    public void Dimensions_Is1024()
        => Assert.Equal(1024, new CohereEmbedder(new HttpClient(), Opts()).Dimensions);

    [Fact]
    public async Task EmbedBatchAsync_EmptyInput_ReturnsEmpty()
    {
        var emb = new CohereEmbedder(new HttpClient(), Opts());
        Assert.Empty(await emb.EmbedBatchAsync([]));
    }

    [Fact]
    public async Task EmbedBatchAsync_ParsesFloatEmbeddings()
    {
        float[] vec = [0.1f, 0.2f, 0.3f];
        string json = $"{{\"embeddings\":{{\"float\":[[{EmbedHttpHelper.FVec(vec)}]]}}}}";
        var emb     = new CohereEmbedder(EmbedHttpHelper.Make(HttpStatusCode.OK, json), Opts());
        var r       = await emb.EmbedBatchAsync(["test"]);
        Assert.Single(r);
        Assert.Equal(vec, r[0]);
    }

    [Fact]
    public void Constructor_NullOptions_Throws()
        => Assert.Throws<ArgumentNullException>(() =>
            new CohereEmbedder(new HttpClient(), null!));
}

// ─────────────────────────────────────────────────────────────────────────────
// E-05 — Mistral (5 tests)
// ─────────────────────────────────────────────────────────────────────────────

public class MistralEmbedderTests
{
    private static MistralEmbedderOptions Opts() => new() { ApiKey = "mstrl" };

    [Fact]
    public void EmbedderType_IsMistral()
        => Assert.Equal(EmbedderType.Mistral,
            new MistralEmbedder(EmbedHttpHelper.Make(HttpStatusCode.OK, "{}"), Opts()).EmbedderType);

    [Fact]
    public void Dimensions_Is1024()
        => Assert.Equal(1024, new MistralEmbedder(EmbedHttpHelper.Make(HttpStatusCode.OK, "{}"), Opts()).Dimensions);

    [Fact]
    public async Task EmbedAsync_ParsesVector()
    {
        float[] vec = [0.9f, 0.1f];
        var emb     = new MistralEmbedder(
            EmbedHttpHelper.Make(HttpStatusCode.OK, EmbedHttpHelper.OpenAISingle(vec)), Opts());
        Assert.Equal(vec, await emb.EmbedAsync("hello"));
    }

    [Fact]
    public async Task EmbedBatchAsync_EmptyInput_ReturnsEmpty()
    {
        var emb = new MistralEmbedder(
            EmbedHttpHelper.Make(HttpStatusCode.OK, "{\"data\":[]}"), Opts());
        Assert.Empty(await emb.EmbedBatchAsync([]));
    }

    [Fact]
    public void Constructor_NullOptions_Throws()
        => Assert.Throws<ArgumentNullException>(() =>
            new MistralEmbedder(new HttpClient(), null!));
}

// ─────────────────────────────────────────────────────────────────────────────
// E-06 — Gemini (5 tests)
// ─────────────────────────────────────────────────────────────────────────────

public class GeminiEmbedderTests
{
    private static GeminiEmbedderOptions Opts() => new() { ApiKey = "gem-key" };

    [Fact]
    public void EmbedderType_IsGoogleGemini()
        => Assert.Equal(EmbedderType.GoogleGemini,
            new GeminiEmbedder(new HttpClient(), Opts()).EmbedderType);

    [Fact]
    public void Dimensions_Is768()
        => Assert.Equal(768, new GeminiEmbedder(new HttpClient(), Opts()).Dimensions);

    [Fact]
    public async Task EmbedAsync_EmptyText_ReturnsZeroVector()
    {
        var emb = new GeminiEmbedder(new HttpClient(), Opts());
        var r   = await emb.EmbedAsync(string.Empty);
        Assert.Equal(768, r.Length);
        Assert.All(r, v => Assert.Equal(0f, v));
    }

    [Fact]
    public async Task EmbedAsync_ParsesValues()
    {
        float[] vec = [1f, 2f, 3f];
        string json = $"{{\"embedding\":{{\"values\":[{EmbedHttpHelper.FVec(vec)}]}}}}";
        var emb     = new GeminiEmbedder(EmbedHttpHelper.Make(HttpStatusCode.OK, json), Opts());
        Assert.Equal(vec, await emb.EmbedAsync("text"));
    }

    [Fact]
    public void Constructor_NullOptions_Throws()
        => Assert.Throws<ArgumentNullException>(() =>
            new GeminiEmbedder(new HttpClient(), null!));
}

// ─────────────────────────────────────────────────────────────────────────────
// E-07 — Voyage AI (5 tests)
// ─────────────────────────────────────────────────────────────────────────────

public class VoyageEmbedderTests
{
    private static VoyageEmbedderOptions Opts() => new() { ApiKey = "voy-key" };

    [Fact]
    public void EmbedderType_IsVoyageAI()
        => Assert.Equal(EmbedderType.VoyageAI,
            new VoyageEmbedder(new HttpClient(), Opts()).EmbedderType);

    [Fact]
    public void Dimensions_Is1024()
        => Assert.Equal(1024, new VoyageEmbedder(new HttpClient(), Opts()).Dimensions);

    [Fact]
    public async Task EmbedBatchAsync_EmptyInput_ReturnsEmpty()
        => Assert.Empty(await new VoyageEmbedder(new HttpClient(), Opts()).EmbedBatchAsync([]));

    [Fact]
    public async Task EmbedBatchAsync_ParsesData()
    {
        float[] vec = [0.3f, 0.7f];
        string json = $"{{\"data\":[{{\"index\":0,\"embedding\":[{EmbedHttpHelper.FVec(vec)}]}}]}}";
        var emb     = new VoyageEmbedder(EmbedHttpHelper.Make(HttpStatusCode.OK, json), Opts());
        var r       = await emb.EmbedBatchAsync(["doc"]);
        Assert.Equal(vec, r[0]);
    }

    [Fact]
    public void Constructor_NullOptions_Throws()
        => Assert.Throws<ArgumentNullException>(() =>
            new VoyageEmbedder(new HttpClient(), null!));
}

// ─────────────────────────────────────────────────────────────────────────────
// E-08 — Jina AI (5 tests)
// ─────────────────────────────────────────────────────────────────────────────

public class JinaEmbedderTests
{
    private static JinaEmbedderOptions Opts() => new() { ApiKey = "jina-key" };

    [Fact]
    public void EmbedderType_IsJinaAI()
        => Assert.Equal(EmbedderType.JinaAI,
            new JinaEmbedder(new HttpClient(), Opts()).EmbedderType);

    [Fact]
    public void Dimensions_Is1024()
        => Assert.Equal(1024, new JinaEmbedder(new HttpClient(), Opts()).Dimensions);

    [Fact]
    public async Task EmbedBatchAsync_EmptyInput_ReturnsEmpty()
        => Assert.Empty(await new JinaEmbedder(new HttpClient(), Opts()).EmbedBatchAsync([]));

    [Fact]
    public async Task EmbedBatchAsync_ParsesData()
    {
        float[] vec = [0.4f, 0.6f];
        string json = $"{{\"data\":[{{\"index\":0,\"embedding\":[{EmbedHttpHelper.FVec(vec)}]}}]}}";
        var emb     = new JinaEmbedder(EmbedHttpHelper.Make(HttpStatusCode.OK, json), Opts());
        var r       = await emb.EmbedBatchAsync(["doc"]);
        Assert.Equal(vec, r[0]);
    }

    [Fact]
    public void Constructor_NullOptions_Throws()
        => Assert.Throws<ArgumentNullException>(() =>
            new JinaEmbedder(new HttpClient(), null!));
}

// ─────────────────────────────────────────────────────────────────────────────
// E-09 — AWS Bedrock Titan (5 tests)
// ─────────────────────────────────────────────────────────────────────────────

public class BedrockTitanEmbedderTests
{
    private static BedrockEmbedderOptions Opts() =>
        new() { EndpointUrl = "http://localhost/" };

    [Fact]
    public void EmbedderType_IsAWSBedrockTitan()
        => Assert.Equal(EmbedderType.AWSBedrockTitan,
            new BedrockTitanEmbedder(new HttpClient(), Opts()).EmbedderType);

    [Fact]
    public void Dimensions_Is1024()
        => Assert.Equal(1024, new BedrockTitanEmbedder(new HttpClient(), Opts()).Dimensions);

    [Fact]
    public async Task EmbedAsync_EmptyText_ReturnsZeroVector()
    {
        var emb = new BedrockTitanEmbedder(new HttpClient(), Opts());
        var r   = await emb.EmbedAsync(string.Empty);
        Assert.Equal(1024, r.Length);
        Assert.All(r, v => Assert.Equal(0f, v));
    }

    [Fact]
    public async Task EmbedAsync_ParsesEmbedding()
    {
        float[] vec = [1f, 2f];
        string json = $"{{\"embedding\":[{EmbedHttpHelper.FVec(vec)}]}}";
        var emb     = new BedrockTitanEmbedder(EmbedHttpHelper.Make(HttpStatusCode.OK, json), Opts());
        Assert.Equal(vec, await emb.EmbedAsync("text"));
    }

    [Fact]
    public void Constructor_NullOptions_Throws()
        => Assert.Throws<ArgumentNullException>(() =>
            new BedrockTitanEmbedder(new HttpClient(), null!));
}

// ─────────────────────────────────────────────────────────────────────────────
// E-10 — Azure OpenAI (5 tests)
// ─────────────────────────────────────────────────────────────────────────────

public class AzureOpenAIEmbedderTests
{
    private static AzureOpenAIEmbedderOptions Opts() =>
        new() { ApiKey = "az-key", Endpoint = "http://localhost/", Deployment = "embed" };

    [Fact]
    public void EmbedderType_IsAzureOpenAI()
        => Assert.Equal(EmbedderType.AzureOpenAI,
            new AzureOpenAIEmbedder(new HttpClient(), Opts()).EmbedderType);

    [Fact]
    public void Dimensions_Is3072()
        => Assert.Equal(3072, new AzureOpenAIEmbedder(new HttpClient(), Opts()).Dimensions);

    [Fact]
    public async Task EmbedBatchAsync_EmptyInput_ReturnsEmpty()
        => Assert.Empty(await new AzureOpenAIEmbedder(new HttpClient(), Opts()).EmbedBatchAsync([]));

    [Fact]
    public async Task EmbedBatchAsync_ParsesData()
    {
        float[] vec = [0.1f, 0.9f];
        var emb     = new AzureOpenAIEmbedder(
            EmbedHttpHelper.Make(HttpStatusCode.OK, EmbedHttpHelper.OpenAIBatch([vec])), Opts());
        var r       = await emb.EmbedBatchAsync(["doc"]);
        Assert.Equal(vec, r[0]);
    }

    [Fact]
    public void Constructor_NullOptions_Throws()
        => Assert.Throws<ArgumentNullException>(() =>
            new AzureOpenAIEmbedder(new HttpClient(), null!));
}

// ─────────────────────────────────────────────────────────────────────────────
// E-11..E-13 — ONNX Embedders (5 tests)
// ─────────────────────────────────────────────────────────────────────────────

public class OnnxEmbedderTests
{
    [Fact]
    public void BertMiniLM_EmbedderType() =>
        Assert.Equal(EmbedderType.OnnxBertMiniLM, new OnnxBertMiniLMEmbedder().EmbedderType);

    [Fact]
    public void BertMiniLM_Dimensions_Is384() =>
        Assert.Equal(384, new OnnxBertMiniLMEmbedder().Dimensions);

    [Fact]
    public void MPNet_EmbedderType() =>
        Assert.Equal(EmbedderType.OnnxMPNet, new OnnxMPNetEmbedder().EmbedderType);

    [Fact]
    public void MPNet_Dimensions_Is768() =>
        Assert.Equal(768, new OnnxMPNetEmbedder().Dimensions);

    [Fact]
    public void Instructor_EmbedderType() =>
        Assert.Equal(EmbedderType.OnnxInstructor, new OnnxInstructorEmbedder().EmbedderType);
}

// ─────────────────────────────────────────────────────────────────────────────
// E-14..E-16 — Ollama / llama.cpp (5 tests)
// ─────────────────────────────────────────────────────────────────────────────

public class OllamaLlamaCppEmbedderTests
{
    [Fact]
    public void OllamaNomic_EmbedderType()
        => Assert.Equal(EmbedderType.OllamaNomic,
            new OllamaNomicEmbedder(new HttpClient()).EmbedderType);

    [Fact]
    public void OllamaMxbai_Dimensions()
        => Assert.Equal(1024, new OllamaMxbaiEmbedder(new HttpClient()).Dimensions);

    [Fact]
    public void LlamaCpp_EmbedderType()
        => Assert.Equal(EmbedderType.LlamaCpp,
            new LlamaCppEmbedder(new HttpClient()).EmbedderType);

    [Fact]
    public async Task OllamaNomic_ParsesEmbeddings()
    {
        float[] vec = [0.5f, 0.5f];
        string json = $"{{\"embeddings\":[[{EmbedHttpHelper.FVec(vec)}]]}}";
        var emb     = new OllamaNomicEmbedder(EmbedHttpHelper.Make(HttpStatusCode.OK, json));
        var r       = await emb.EmbedBatchAsync(["text"]);
        Assert.Equal(vec, r[0]);
    }

    [Fact]
    public async Task LlamaCpp_EmptyText_ReturnsZeroVector()
    {
        var emb = new LlamaCppEmbedder(new HttpClient(), new LlamaCppEmbedderOptions { Dimensions = 4 });
        var r   = await emb.EmbedAsync(string.Empty);
        Assert.Equal(4, r.Length);
        Assert.All(r, v => Assert.Equal(0f, v));
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// E-17..E-18 — HuggingFace / NVIDIA NIM (5 tests)
// ─────────────────────────────────────────────────────────────────────────────

public class HuggingFaceNimEmbedderTests
{
    [Fact]
    public void HuggingFace_EmbedderType()
        => Assert.Equal(EmbedderType.HuggingFaceApi,
            new HuggingFaceEmbedder(new HttpClient(),
                new HuggingFaceEmbedderOptions { ApiKey = "k" }).EmbedderType);

    [Fact]
    public void NvidiaNim_EmbedderType()
        => Assert.Equal(EmbedderType.NvidiaNIM,
            new NvidiaNimEmbedder(new HttpClient(),
                new NvidiaNimEmbedderOptions { ApiKey = "k" }).EmbedderType);

    [Fact]
    public void NvidiaNim_Dimensions_Is1024()
        => Assert.Equal(1024,
            new NvidiaNimEmbedder(new HttpClient(),
                new NvidiaNimEmbedderOptions { ApiKey = "k" }).Dimensions);

    [Fact]
    public async Task HuggingFace_Parse1DArray()
    {
        float[] vec = [0.1f, 0.2f, 0.3f];
        // HF returns a 1-D float array for pre-pooled models.
        string json = $"[{EmbedHttpHelper.FVec(vec)}]";
        var emb     = new HuggingFaceEmbedder(
            EmbedHttpHelper.Make(HttpStatusCode.OK, json),
            new HuggingFaceEmbedderOptions { ApiKey = "k", Dimensions = 3 });
        Assert.Equal(vec, await emb.EmbedAsync("hi"));
    }

    [Fact]
    public async Task NvidiaNim_EmbedBatchAsync_EmptyInput_ReturnsEmpty()
        => Assert.Empty(await new NvidiaNimEmbedder(new HttpClient(),
            new NvidiaNimEmbedderOptions { ApiKey = "k" }).EmbedBatchAsync([]));
}

// ─────────────────────────────────────────────────────────────────────────────
// E-19..E-25 — ONNX Specialty Embedders (7 tests, one per type)
// ─────────────────────────────────────────────────────────────────────────────

public class OnnxSpecialtyEmbedderTests
{
    [Fact]
    public void MultilingualE5_Type() =>
        Assert.Equal(EmbedderType.MultilingualE5, new MultilingualE5Embedder().EmbedderType);

    [Fact]
    public void Bge_Type() =>
        Assert.Equal(EmbedderType.BGE, new BgeEmbedder().EmbedderType);

    [Fact]
    public void Gte_Type() =>
        Assert.Equal(EmbedderType.GTE, new GteEmbedder().EmbedderType);

    [Fact]
    public void Solar_Type() =>
        Assert.Equal(EmbedderType.SOLAR, new SolarEmbedder().EmbedderType);

    [Fact]
    public void M3E_Type() =>
        Assert.Equal(EmbedderType.M3E, new M3EEmbedder().EmbedderType);

    [Fact]
    public void BgeM3_Type() =>
        Assert.Equal(EmbedderType.BAGEM3, new BgeM3Embedder().EmbedderType);

    [Fact]
    public void NomicV15_Type() =>
        Assert.Equal(EmbedderType.NomicV15, new NomicV15Embedder().EmbedderType);
}
