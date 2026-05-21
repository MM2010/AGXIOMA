namespace AGXIOME.Shared.Models;

// ─────────────────────────────────────────────────────────────────────────────
// Embedder types — 25 embedders
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Typed embedder discriminator. Values correspond to specific model checkpoints and output dimensions.</summary>
public enum EmbedderType
{
    OpenAISmall = 0,      // text-embedding-3-small 1536d
    OpenAILarge = 1,      // text-embedding-3-large 3072d
    Anthropic = 2,
    Cohere = 3,           // embed-english-v3.0 1024d
    Mistral = 4,          // mistral-embed 1024d
    GoogleGemini = 5,     // text-embedding-004 768d
    VoyageAI = 6,         // voyage-3-large 1024d
    JinaAI = 7,           // jina-embeddings-v3 1024d
    AWSBedrockTitan = 8,  // titan-embed-text-v2 1024d
    AzureOpenAI = 9,      // text-embedding-3-large 3072d
    OnnxBertMiniLM = 10,  // all-MiniLM-L6-v2 384d
    OnnxMPNet = 11,       // all-mpnet-base-v2 768d
    OnnxInstructor = 12,  // instructor-xl 768d
    OllamaNomic = 13,     // nomic-embed-text 768d
    OllamaMxbai = 14,     // mxbai-embed-large 1024d
    LlamaCpp = 15,        // server GGUF 768+d
    HuggingFaceApi = 16,
    NvidiaNIM = 17,       // NV-Embed-QA 1024d
    MultilingualE5 = 18,  // multilingual-e5-large 1024d
    BGE = 19,             // bge-large-en-v1.5 1024d
    GTE = 20,             // gte-large-en-v1.5 1024d
    SOLAR = 21,           // solar-embedding 4096d
    M3E = 22,             // m3e-large 1024d
    BAGEM3 = 23,          // bge-m3 1024d
    NomicV15 = 24,        // nomic-embed-text-v1.5 768d
}
