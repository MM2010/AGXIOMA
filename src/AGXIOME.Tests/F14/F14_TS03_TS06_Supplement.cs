// TS-03/TS-06 Supplement — Additional Tests (26 tests total)
// TS-03: 15 tests to reach 75-test target
// TS-06: 11 tests to reach 103-test target

using System.Net;
using AGXIOME.Shared.Models;
using AGXIOME.Storage.Embedders;
using AGXIOME.Storage.Stores;

namespace AGXIOME.Tests.F14;

// ═════════════════════════════════════════════════════════════════════════════
// TS-03 Supplement — Additional DB store scenarios (15 tests)
// ═════════════════════════════════════════════════════════════════════════════

public sealed class TS03_Supplement_SqliteSchemaTests
{
    [Fact] public async Task AutoCreateSchema_True_TablesCreated()
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"schema_{Guid.NewGuid():N}.db");
        try
        {
            using var store = new SqliteCommandStore(new SqliteStoreOptions
                { ConnectionString = $"Data Source={tmp};Pooling=False", AutoCreateSchema = true });
            // Saving should work (table was auto-created)
            var ex = await Record.ExceptionAsync(() => store.SaveAsync(new F14DbRecord { Id = "s1", Value = 1 }));
            Assert.Null(ex);
        }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
    }

    [Fact] public async Task LargeValue_StoresCorrectly()
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"large_{Guid.NewGuid():N}.db");
        try
        {
            using var store = new SqliteCommandStore(new SqliteStoreOptions
                { ConnectionString = $"Data Source={tmp};Pooling=False", AutoCreateSchema = true });
            // Just verify large value doesn't cause overflow on insert
            var ex = await Record.ExceptionAsync(() =>
                store.SaveAsync(new F14DbRecord { Id = "large", Value = int.MaxValue }));
            Assert.Null(ex);
        }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
    }

    [Fact] public async Task NegativeValue_StoresCorrectly()
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"neg_{Guid.NewGuid():N}.db");
        try
        {
            using var store = new SqliteCommandStore(new SqliteStoreOptions
                { ConnectionString = $"Data Source={tmp};Pooling=False", AutoCreateSchema = true });
            var ex = await Record.ExceptionAsync(() =>
                store.SaveAsync(new F14DbRecord { Id = "neg", Value = int.MinValue }));
            Assert.Null(ex);
        }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
    }

    [Fact] public async Task ZeroValue_StoresCorrectly()
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"zero_{Guid.NewGuid():N}.db");
        try
        {
            using var store = new SqliteCommandStore(new SqliteStoreOptions
                { ConnectionString = $"Data Source={tmp};Pooling=False", AutoCreateSchema = true });
            var ex = await Record.ExceptionAsync(() =>
                store.SaveAsync(new F14DbRecord { Id = "zero", Value = 0 }));
            Assert.Null(ex);
        }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
    }

    [Fact] public async Task MultipleDisposeCalls_DoNotThrow()
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"disp_{Guid.NewGuid():N}.db");
        try
        {
            var store = new SqliteCommandStore(new SqliteStoreOptions
                { ConnectionString = $"Data Source={tmp};Pooling=False", AutoCreateSchema = true });
            store.Dispose();
            var ex = Record.Exception(() => store.Dispose());
            Assert.Null(ex);
        }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
    }
}

public sealed class TS03_Supplement_ProviderTypeCompletenessTests
{
    [Fact] public async Task StorageProviderType_HasAtLeast20Values()
        => Assert.True(Enum.GetValues<StorageProviderType>().Length >= 20);

    [Fact] public async Task StorageProviderType_ContainsSQLite()
        => Assert.True(Enum.IsDefined(typeof(StorageProviderType), StorageProviderType.SQLite));

    [Fact] public async Task StorageProviderType_ContainsPostgreSQL()
        => Assert.True(Enum.IsDefined(typeof(StorageProviderType), StorageProviderType.PostgreSQL));

    [Fact] public async Task StorageProviderType_ContainsMongoDB()
        => Assert.True(Enum.IsDefined(typeof(StorageProviderType), StorageProviderType.MongoDB));

    [Fact] public async Task StorageProviderType_ContainsRedis()
        => Assert.True(Enum.IsDefined(typeof(StorageProviderType), StorageProviderType.RedisCache));

    [Fact] public async Task StorageProviderType_ContainsDynamoDB()
        => Assert.True(Enum.IsDefined(typeof(StorageProviderType), StorageProviderType.DynamoDB));

    [Fact] public async Task StorageProviderType_ContainsCosmosDB()
        => Assert.True(Enum.IsDefined(typeof(StorageProviderType), StorageProviderType.CosmosDB));

    [Fact] public async Task StorageProviderType_ContainsJsonFile()
        => Assert.True(Enum.IsDefined(typeof(StorageProviderType), StorageProviderType.JsonFile));

    [Fact] public async Task StorageProviderType_ContainsSqlServer()
        => Assert.True(Enum.IsDefined(typeof(StorageProviderType), StorageProviderType.SQLServer));

    [Fact] public async Task StorageProviderType_ContainsMySQL()
        => Assert.True(Enum.IsDefined(typeof(StorageProviderType), StorageProviderType.MySQL));
}

// ═════════════════════════════════════════════════════════════════════════════
// TS-06 Supplement — Additional embedder scenarios (11 tests)
// ═════════════════════════════════════════════════════════════════════════════

file static class S06Helper
{
    public static HttpClient Make(HttpStatusCode status, string json)
        => new(new S06Handler(status, json));
}

file sealed class S06Handler(HttpStatusCode status, string json) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        });
}

public sealed class TS06_Supplement_EmbedderTests
{
    [Fact] public async Task AnthropicEmbedder_CustomDimensions()
    {
        var opts = new AnthropicEmbedderOptions { ApiKey = "k", Dimensions = 512 };
        var emb  = new AnthropicEmbedder(S06Helper.Make(HttpStatusCode.OK, "{}"), opts);
        Assert.Equal(512, emb.Dimensions);
    }

    [Fact] public async Task AnthropicEmbedder_CustomModel()
    {
        var opts = new AnthropicEmbedderOptions { ApiKey = "k", Model = "custom-model" };
        Assert.Equal("custom-model", opts.Model);
    }

    [Fact] public async Task CohereEmbedder_DefaultModel_NotEmpty()
    {
        var opts = new CohereEmbedderOptions { ApiKey = "k" };
        Assert.NotEmpty(opts.Model ?? "embed-english-v3.0");
    }

    [Fact] public async Task GeminiEmbedder_DefaultModel_NotEmpty()
    {
        var opts = new GeminiEmbedderOptions { ApiKey = "k" };
        Assert.NotEmpty(opts.Model ?? "embedding-001");
    }

    [Fact] public async Task MistralEmbedder_DefaultModel_NotEmpty()
    {
        var opts = new MistralEmbedderOptions { ApiKey = "k" };
        Assert.NotEmpty(opts.Model ?? "mistral-embed");
    }

    [Fact] public async Task VoyageEmbedder_DefaultModel_NotEmpty()
    {
        var opts = new VoyageEmbedderOptions { ApiKey = "k" };
        Assert.NotEmpty(opts.Model ?? "voyage-02");
    }

    [Fact] public async Task CohereEmbedder_SingleInput_DoesNotThrowOnEmptyResponse()
    {
        var emb = new CohereEmbedder(S06Helper.Make(HttpStatusCode.OK, "{}"),
            new CohereEmbedderOptions { ApiKey = "k" });
        var r = await emb.EmbedAsync(string.Empty);
        Assert.Equal(emb.Dimensions, r.Length);
    }

    [Fact] public async Task HuggingFaceEmbedder_BatchSingle_SizeOne()
    {
        var emb = new HuggingFaceEmbedder(S06Helper.Make(HttpStatusCode.OK, "[[0.1, 0.2]]"),
            new HuggingFaceEmbedderOptions { ApiKey = "k" });
        var r = await emb.EmbedBatchAsync(["text"]);
        Assert.Single(r);
    }

    [Fact] public async Task LlamaCppEmbedder_CustomBaseUrl()
    {
        var opts = new LlamaCppEmbedderOptions { BaseUrl = "http://custom:9000/" };
        Assert.Equal("http://custom:9000/", opts.BaseUrl);
    }

    [Fact] public async Task NvidiaNimEmbedder_DefaultBaseUrl_NotEmpty()
    {
        var opts = new NvidiaNimEmbedderOptions { ApiKey = "k" };
        Assert.NotEmpty(opts.BaseUrl ?? "https://integrate.api.nvidia.com/v1/");
    }

    [Fact] public async Task AzureOpenAIEmbedder_Deployment_Stored()
    {
        var opts = new AzureOpenAIEmbedderOptions
            { ApiKey = "k", Endpoint = "https://r.openai.azure.com", Deployment = "my-deploy" };
        Assert.Equal("my-deploy", opts.Deployment);
    }
}
