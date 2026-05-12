namespace AGXIOME.Shared.Models;

// ─────────────────────────────────────────────────────────────────────────────
// LLM / Model providers — 50 providers, zero hardcoded strings in routing code
// ─────────────────────────────────────────────────────────────────────────────
public enum LLMProviderType
{
    // Existing 8
    OpenAI = 0,
    Anthropic = 1,
    Gemini = 2,
    DeepSeek = 3,
    Ollama = 4,
    AzureOpenAI = 5,
    Local = 6,
    Custom = 7,

    // New 42 — M-01..M-50
    Groq = 10,
    Mistral = 11,
    XAIGrok = 12,
    Cohere = 13,
    Perplexity = 14,
    TogetherAI = 15,
    FireworksAI = 16,
    Cerebras = 17,
    AWSBedrock = 18,
    AzureAIFoundry = 19,
    IBMWatsonX = 20,
    SambaNova = 21,
    OpenRouter = 22,
    LiteLLM = 23,
    AIMLApi = 24,
    NovitaAI = 25,
    DeepInfra = 26,
    Replicate = 27,
    RekaAI = 28,
    Modal = 29,
    LlamaCpp = 30,
    LMStudio = 31,
    OllamaResponses = 32,
    LocalAI = 33,
    GPT4All = 34,
    TextGenWebUI = 35,
    KoboldCPP = 36,
    HuggingFaceInference = 37,
    HuggingFaceTGI = 38,
    AI21Labs = 39,
    Writer = 40,
    NvidiaNIM = 41,
    Anyscale = 42,
    OctoAI = 43,
    LeptonAI = 44,
    FriendliAI = 45,
    MoonshotKimi = 46,
    ZhipuGLM = 47,
    Baichuan = 48,
    ByteDanceDoubao = 49,
    AlibabaQwen = 50,
    BaiduERNIE = 51,
    TencentHunyuan = 52,
    YiAI = 53,
    AI21Jamba = 54,
    SnowflakeArctic = 55,
    Phind = 56,
    YouCom = 57,
    Hyperbolic = 58,
    Featherless = 59,
}

// ─────────────────────────────────────────────────────────────────────────────
// Storage / DB providers — 25 providers
// ─────────────────────────────────────────────────────────────────────────────
public enum StorageProviderType
{
    // Existing 5
    PostgreSQL = 0,
    MySQL = 1,
    MariaDB = 2,
    MongoDB = 3,
    RedisCache = 4,

    // New 25 — DB-01..DB-25
    SQLite = 10,
    SQLServer = 11,
    SQLServerLocalDB = 12,
    Neon = 13,
    Supabase = 14,
    SingleStore = 15,
    CockroachDB = 16,
    TimescaleDB = 17,
    DynamoDB = 18,
    CosmosDB = 19,
    Firestore = 20,
    SurrealDB = 21,
    RavenDB = 22,
    Couchbase = 23,
    RedisSession = 24,
    Memcached = 25,
    Hazelcast = 26,
    Etcd = 27,
    ClickHouse = 28,
    InfluxDB = 29,
    QuestDB = 30,
    DuckDB = 31,
    Elasticsearch = 32,
    Meilisearch = 33,
    JsonFile = 34,
}

// ─────────────────────────────────────────────────────────────────────────────
// Vector DB providers — 30 providers
// ─────────────────────────────────────────────────────────────────────────────
public enum VectorDbProviderType
{
    // Existing 1
    PgVector = 0,

    // New 30 — VD-01..VD-30
    Pinecone = 10,
    WeaviateCloud = 11,
    QdrantCloud = 12,
    MilvusCloud = 13,
    ChromaDB = 14,
    SupabasePgVector = 15,
    NeonPgVector = 16,
    TimescalePgVector = 17,
    CockroachPgVector = 18,
    SingleStoreVector = 19,
    QdrantSelfHosted = 20,
    WeaviateSelfHosted = 21,
    MilvusSelfHosted = 22,
    ChromaSelfHosted = 23,
    ElasticsearchDense = 24,
    MeilisearchVector = 25,
    Vald = 26,
    Vespa = 27,
    SqliteVec = 28,
    LanceDB = 29,
    DuckDBVector = 30,
    RedisStack = 31,
    MongoDBAtlas = 32,
    CosmosDBVCore = 33,
    HNSWLib = 34,
    FAISS = 35,
    Annoy = 36,
    ScaNN = 37,
    RocksDBHNSW = 38,
    SQLServerVector = 39,
}

// ─────────────────────────────────────────────────────────────────────────────
// Embedder types — 25 embedders
// ─────────────────────────────────────────────────────────────────────────────
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

// ─────────────────────────────────────────────────────────────────────────────
// Document Reader types — 28 readers
// ─────────────────────────────────────────────────────────────────────────────
public enum DocumentReaderType
{
    Pdf = 0,
    Docx = 1,
    Xlsx = 2,
    Pptx = 3,
    Csv = 4,
    Json = 5,
    Xml = 6,
    Markdown = 7,
    Html = 8,
    PlainText = 9,
    Yaml = 10,
    Rtf = 11,
    Website = 12,
    Wikipedia = 13,
    Arxiv = 14,
    PubMed = 15,
    YoutubeTranscript = 16,
    Firecrawl = 17,
    AWSS3 = 18,
    AzureBlob = 19,
    GCS = 20,
    GitHub = 21,
    SharePoint = 22,
    ImageOCR = 23,
    AudioWhisper = 24,
    Epub = 25,
    Notion = 26,
    Confluence = 27,
}

// ─────────────────────────────────────────────────────────────────────────────
// Chunker types — 12 chunkers
// ─────────────────────────────────────────────────────────────────────────────
public enum ChunkerType
{
    // Existing 3
    Fixed = 0,
    Heading = 1,
    Sentence = 2,

    // New 9 — C-01..C-09
    Recursive = 10,
    Semantic = 11,
    Agentic = 12,
    Code = 13,
    Document = 14,
    CsvRow = 15,
    JsonPath = 16,
    SlidingWindow = 17,
    Hybrid = 18,
}

// ─────────────────────────────────────────────────────────────────────────────
// Reranker types — 8 rerankers
// ─────────────────────────────────────────────────────────────────────────────
public enum RerankerType
{
    CohereEnglish = 0,
    CohereMultilingual = 1,
    JinaV2 = 2,
    VoyageRerank2 = 3,
    BgeOnnx = 4,
    CrossEncoderOnnx = 5,
    MixedBreadOnnx = 6,
    LLMReranker = 7,
}

// ─────────────────────────────────────────────────────────────────────────────
// Toolkit category — 12 categories
// ─────────────────────────────────────────────────────────────────────────────
public enum ToolkitCategory
{
    Communication = 0,
    Productivity = 1,
    DataAnalysis = 2,
    Development = 3,
    AiMl = 4,
    Finance = 5,
    CloudInfra = 6,
    WebSocial = 7,
    SearchKnowledge = 8,
    FileStorage = 9,
    GamingCreative = 10,
    Enterprise = 11,
}

// ─────────────────────────────────────────────────────────────────────────────
// Auth / Security types
// ─────────────────────────────────────────────────────────────────────────────
public enum AuthSchemeType
{
    Jwt = 0,
    ApiKey = 1,
    OAuth2 = 2,
    JweEncrypted = 3,
}

public enum RbacRole
{
    Admin = 0,
    Operator = 1,
    Developer = 2,
    ReadOnly = 3,
}

public enum RbacAction
{
    Read = 0,
    Write = 1,
    Execute = 2,
    Delete = 3,
    Admin = 4,
}

public enum RbacResource
{
    Agent = 0,
    Session = 1,
    Team = 2,
    Workflow = 3,
    Knowledge = 4,
    Tool = 5,
    Metrics = 6,
    Config = 7,
}

// ─────────────────────────────────────────────────────────────────────────────
// Background job / runtime types
// ─────────────────────────────────────────────────────────────────────────────
public enum BackgroundJobType
{
    RunExecution = 0,
    SessionCleanup = 1,
    TokenRotation = 2,
    KnowledgeIngest = 3,
}

public enum BackgroundJobStatus
{
    Queued = 0,
    Running = 1,
    Completed = 2,
    Failed = 3,
    Cancelled = 4,
    Retrying = 5,
}

public enum ResumeStrategy
{
    Restart = 0,
    ContinueFromCheckpoint = 1,
    SkipFailed = 2,
}

// ─────────────────────────────────────────────────────────────────────────────
// Response cache / token counting
// ─────────────────────────────────────────────────────────────────────────────
public enum CacheStrategy
{
    None = 0,
    InMemory = 1,
    Redis = 2,
    Hybrid = 3,
}

public enum TokenizerAlgorithm
{
    Cl100kBase = 0,   // GPT-4 / GPT-3.5
    O200kBase = 1,    // o1/o3/gpt-4o
    Llama3 = 2,
    Llama2 = 3,
    Mistral = 4,
    Gemini = 5,
    Generic = 6,
}

// ─────────────────────────────────────────────────────────────────────────────
// CLI command types
// ─────────────────────────────────────────────────────────────────────────────
public enum CliCommand
{
    Run = 0,
    List = 1,
    Session = 2,
    Admin = 3,
    Config = 4,
}

public enum CliOutputFormat
{
    Plain = 0,
    Json = 1,
    Table = 2,
}

// ─────────────────────────────────────────────────────────────────────────────
// Model compatibility / deprecation
// ─────────────────────────────────────────────────────────────────────────────
public enum ModelCompatibilityStatus
{
    Supported = 0,
    Deprecated = 1,
    Removed = 2,
    Preview = 3,
}

// ─────────────────────────────────────────────────────────────────────────────
// Multi-framework bridge types
// ─────────────────────────────────────────────────────────────────────────────
public enum BridgeFramework
{
    LangChain = 0,
    SemanticKernel = 1,
    ClaudeSDK = 2,
}
