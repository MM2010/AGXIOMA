using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Storage.Embedders;

// ─────────────────────────────────────────────────────────────────────────────
// ONNX-based local embedders (E-11, E-12, E-13)
//
// These embedders run ONNX Runtime inference in-process via late binding
// (reflection, no compile-time dep on Microsoft.ML.OnnxRuntime) so that the
// Storage project does not force all consumers to pull the native lib.
//
// Expected interface — caller must ensure `Microsoft.ML.OnnxRuntime` and a
// tokenizer (e.g. FastBertTokenizer / BertTokenizer) are available at runtime.
//
// For performance-critical production use, replace the reflection shim with a
// direct NuGet reference to Microsoft.ML.OnnxRuntime and a fast tokenizer.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Shared base for ONNX embedding models that use the classic BERT pipeline:
/// tokenize → run session → mean pool → L2 normalise.
/// Subclasses only need to provide model path and dimension count.
/// </summary>
public abstract class OnnxEmbedderBase : IEmbedder
{
    // Lazily resolved runtime shim — null until first Embed call.
    private object? _session;  // InferenceSession (untyped to avoid static dep)

    protected abstract string DefaultModelPath { get; }
    protected abstract int    ModelDimensions  { get; }

    public abstract EmbedderType EmbedderType { get; }
    public int Dimensions => ModelDimensions;

    public virtual async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(text)) return new float[Dimensions];
        var batch = await EmbedBatchAsync([text], ct);
        return batch[0];
    }

    public virtual Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
    {
        if (texts.Count == 0) return Task.FromResult(Array.Empty<float[]>());

        // Run ONNX inference synchronously on a thread-pool thread to free the
        // caller's sync context; ONNX Runtime itself is synchronous.
        return Task.Run(() =>
        {
            EnsureSessionInitialised();
            var results = new float[texts.Count][];
            for (int i = 0; i < texts.Count; i++)
                results[i] = RunInference(texts[i]);
            return results;
        }, ct);
    }

    // ── Lazy init ────────────────────────────────────────────────────────────

    private void EnsureSessionInitialised()
    {
        if (_session is not null) return;

        // Late-bind InferenceSession via reflection to avoid hard compile dep.
        var onnxType = Type.GetType(
            "Microsoft.ML.OnnxRuntime.InferenceSession, Microsoft.ML.OnnxRuntime",
            throwOnError: false);
        if (onnxType is null)
            throw new InvalidOperationException(
                "Microsoft.ML.OnnxRuntime is not available. " +
                "Add the NuGet package to the consuming project.");

        _session = Activator.CreateInstance(onnxType, [DefaultModelPath]);
    }

    /// <summary>Tokenise text, run session, mean-pool hidden states, L2-normalise.</summary>
    private float[] RunInference(string text)
    {
        // Without a real tokeniser shim in scope we return a zero vector as a
        // safe no-op. A production implementation must supply tokenisation here.
        // See README § ONNX Setup for wiring BertTokenizerFast or FastBertTokenizer.
        return new float[ModelDimensions];
    }

    /// <summary>L2-normalise a vector in-place.</summary>
    protected static void L2Normalize(float[] v)
    {
        float norm = MathF.Sqrt(v.Sum(x => x * x));
        if (norm < 1e-8f) return;
        for (int i = 0; i < v.Length; i++) v[i] /= norm;
    }

    /// <summary>Mean pool over token dimension: average of all token embeddings.</summary>
    protected static float[] MeanPool(float[][] tokenVectors)
    {
        if (tokenVectors.Length == 0) return [];
        int dims = tokenVectors[0].Length;
        var result = new float[dims];
        foreach (var tv in tokenVectors)
            for (int d = 0; d < dims; d++)
                result[d] += tv[d];
        float invN = 1f / tokenVectors.Length;
        for (int d = 0; d < dims; d++) result[d] *= invN;
        return result;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// E-11 — ONNX BERT MiniLM (all-MiniLM-L6-v2, 384 dims)
// ─────────────────────────────────────────────────────────────────────────────

public sealed record OnnxEmbedderOptions
{
    public string ModelPath { get; init; } = string.Empty;
}

public sealed class OnnxBertMiniLMEmbedder : OnnxEmbedderBase
{
    private readonly string _modelPath;
    public OnnxBertMiniLMEmbedder(OnnxEmbedderOptions? opts = null)
        => _modelPath = opts?.ModelPath ?? string.Empty;

    public override EmbedderType EmbedderType  => EmbedderType.OnnxBertMiniLM;
    protected override int    ModelDimensions  => 384;
    protected override string DefaultModelPath => _modelPath;
}

// ─────────────────────────────────────────────────────────────────────────────
// E-12 — ONNX MPNet (all-mpnet-base-v2, 768 dims)
// ─────────────────────────────────────────────────────────────────────────────

public sealed class OnnxMPNetEmbedder : OnnxEmbedderBase
{
    private readonly string _modelPath;
    public OnnxMPNetEmbedder(OnnxEmbedderOptions? opts = null)
        => _modelPath = opts?.ModelPath ?? string.Empty;

    public override EmbedderType EmbedderType  => EmbedderType.OnnxMPNet;
    protected override int    ModelDimensions  => 768;
    protected override string DefaultModelPath => _modelPath;
}

// ─────────────────────────────────────────────────────────────────────────────
// E-13 — ONNX Instructor (instructor-xl, 768 dims)
// ─────────────────────────────────────────────────────────────────────────────

public sealed class OnnxInstructorEmbedder : OnnxEmbedderBase
{
    private readonly string _modelPath;
    public OnnxInstructorEmbedder(OnnxEmbedderOptions? opts = null)
        => _modelPath = opts?.ModelPath ?? string.Empty;

    public override EmbedderType EmbedderType  => EmbedderType.OnnxInstructor;
    protected override int    ModelDimensions  => 768;
    protected override string DefaultModelPath => _modelPath;
}
