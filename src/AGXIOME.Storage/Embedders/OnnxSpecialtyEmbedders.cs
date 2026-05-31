using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Storage.Embedders;

// ─────────────────────────────────────────────────────────────────────────────
// ONNX Specialty Embedders (E-19 … E-25)
//
// All inherit from OnnxEmbedderBase which handles the ONNX session shim,
// mean-pooling, and L2 normalisation. Model-specific subclasses only declare:
//  - EmbedderType enum value
//  - model dimensions
//  - default model path (overridable via OnnxEmbedderOptions)
//
// For production, provide the model .onnx file path in OnnxEmbedderOptions.
// ─────────────────────────────────────────────────────────────────────────────

// ── E-19 — Multilingual E5 (multilingual-e5-large, 1024 dims) ────────────────
/// <summary>Core implementation for the AGXIOME agentic orchestration framework.</summary>
public sealed class MultilingualE5Embedder : OnnxEmbedderBase
{
    private readonly string _modelPath;
    public MultilingualE5Embedder(OnnxEmbedderOptions? opts = null)
        => _modelPath = opts?.ModelPath ?? string.Empty;

    public override EmbedderType EmbedderType  => EmbedderType.MultilingualE5;
    protected override int    ModelDimensions  => 1024;
    protected override string DefaultModelPath => _modelPath;
}

// ── E-20 — BGE Embedding (bge-large-en-v1.5, 1024 dims) ─────────────────────
public sealed class BgeEmbedder : OnnxEmbedderBase
{
    private readonly string _modelPath;
    public BgeEmbedder(OnnxEmbedderOptions? opts = null)
        => _modelPath = opts?.ModelPath ?? string.Empty;

    public override EmbedderType EmbedderType  => EmbedderType.BGE;
    protected override int    ModelDimensions  => 1024;
    protected override string DefaultModelPath => _modelPath;
}

// ── E-21 — GTE Embedding (gte-large-en-v1.5, 1024 dims) ─────────────────────
public sealed class GteEmbedder : OnnxEmbedderBase
{
    private readonly string _modelPath;
    public GteEmbedder(OnnxEmbedderOptions? opts = null)
        => _modelPath = opts?.ModelPath ?? string.Empty;

    public override EmbedderType EmbedderType  => EmbedderType.GTE;
    protected override int    ModelDimensions  => 1024;
    protected override string DefaultModelPath => _modelPath;
}

// ── E-22 — SOLAR Embedding (solar-embedding, 4096 dims) ──────────────────────
public sealed class SolarEmbedder : OnnxEmbedderBase
{
    private readonly string _modelPath;
    public SolarEmbedder(OnnxEmbedderOptions? opts = null)
        => _modelPath = opts?.ModelPath ?? string.Empty;

    public override EmbedderType EmbedderType  => EmbedderType.SOLAR;
    protected override int    ModelDimensions  => 4096;
    protected override string DefaultModelPath => _modelPath;
}

// ── E-23 — M3E Embedding (m3e-large, 1024 dims) ──────────────────────────────
public sealed class M3EEmbedder : OnnxEmbedderBase
{
    private readonly string _modelPath;
    public M3EEmbedder(OnnxEmbedderOptions? opts = null)
        => _modelPath = opts?.ModelPath ?? string.Empty;

    public override EmbedderType EmbedderType  => EmbedderType.M3E;
    protected override int    ModelDimensions  => 1024;
    protected override string DefaultModelPath => _modelPath;
}

// ── E-24 — BAAI/bge-m3 (multi-lingual, 1024 dims) ────────────────────────────
public sealed class BgeM3Embedder : OnnxEmbedderBase
{
    private readonly string _modelPath;
    public BgeM3Embedder(OnnxEmbedderOptions? opts = null)
        => _modelPath = opts?.ModelPath ?? string.Empty;

    public override EmbedderType EmbedderType  => EmbedderType.BAGEM3;
    protected override int    ModelDimensions  => 1024;
    protected override string DefaultModelPath => _modelPath;
}

// ── E-25 — Nomic Embed v1.5 (nomic-embed-text-v1.5, 768 dims) ────────────────
public sealed class NomicV15Embedder : OnnxEmbedderBase
{
    private readonly string _modelPath;
    public NomicV15Embedder(OnnxEmbedderOptions? opts = null)
        => _modelPath = opts?.ModelPath ?? string.Empty;

    public override EmbedderType EmbedderType  => EmbedderType.NomicV15;
    protected override int    ModelDimensions  => 768;
    protected override string DefaultModelPath => _modelPath;
}
