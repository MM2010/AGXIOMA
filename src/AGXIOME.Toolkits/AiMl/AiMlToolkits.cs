// T-68..T-79 — AI/ML Toolkits
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Text.Json;
using AGXIOME.Core.Utils;
using AGXIOME.Shared.Errors;

namespace AGXIOME.Toolkits.AiMl;

// ── shared HTTP client ─────────────────────────────────────────────────────
file static class Http
{
    internal static HttpClient Client { get; private set; } = new() { Timeout = TimeSpan.FromSeconds(120) }; // models are slow

    internal static IDisposable UseClient(HttpClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        var previous = Client;
        Client = client;
        return new RestoreClient(previous);
    }

    private sealed class RestoreClient(HttpClient previous) : IDisposable
    {
        public void Dispose() => Client = previous;
    }
}

// T-68 — OpenAI Vision (GPT-4o / gpt-4-vision — api_key required)
[Toolkit("openai_vision","OpenAI Vision",ToolkitCategory.AiMl)]
/// <summary>Core implementation for the AGXIOME agentic orchestration framework.</summary>
public sealed class OpenAiVisionToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"openai_vision";public override string Name=>"OpenAI Vision";
    public override ToolkitCategory Category=>ToolkitCategory.AiMl;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("api_key"));
    internal static IDisposable UseHttpClient(HttpClient client) => Http.UseClient(client);
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var key=GetRequiredSetting("api_key");
        var model=GetParam<string>(p,"model")??GetSetting("model")??"gpt-4o";
        var imageUrl=GetParam<string>(p,"image_url")??GetParam<string>(p,"url");
        var imageBase64=GetParam<string>(p,"image_base64");
        ValidateImageInput(imageUrl, imageBase64);
        var prompt=GetParam<string>(p,"prompt")??GetDefaultPrompt(a);
        var detail=GetParam<string>(p,"detail")??"auto";
        object imageContent=imageUrl is not null
            ? ImageUtil.BuildOpenAIImageContent(imageUrl, detail)
            : ImageUtil.BuildOpenAIImageContent($"data:image/jpeg;base64,{imageBase64}", detail);
        var payload=new{model,messages=new[]{new{role="user",content=new object[]{new{type="text",text=prompt},imageContent}}},max_tokens=GetParam<int?>(p,"max_tokens")??500};
        var result = await OaiPost("chat/completions",payload,key,ct).ConfigureAwait(false);
        return NormalizeVisionResult(a, result);
    }

    private static string GetDefaultPrompt(string action) => action switch
    {
        "analyze_image" => "Analyze this image and summarize the important details.",
        "describe" => "Describe this image clearly and concisely.",
        "ocr" => "Extract all readable text from this image and return plain text.",
        "object_detect" => "Identify the main objects visible in this image and return a concise list.",
        _ => throw AgxiomeToolkitException.UnknownAction("openai_vision", action)
    };

    private static void ValidateImageInput(string? imageUrl, string? imageBase64)
    {
        var hasUrl = !string.IsNullOrWhiteSpace(imageUrl);
        var hasBase64 = !string.IsNullOrWhiteSpace(imageBase64);

        if (hasUrl == hasBase64)
            throw AgxiomeToolkitException.MissingParameter("openai_vision", "image_url|image_base64");

        if (hasUrl && !ImageUtil.ValidateImageUrl(imageUrl!))
            throw AgxiomeToolkitException.MissingParameter("openai_vision", "image_url");

        if (!hasBase64)
            return;

        try
        {
            _ = Convert.FromBase64String(imageBase64!);
        }
        catch (FormatException ex)
        {
            throw AgxiomeToolkitException.ExecutionFailed("openai_vision", "validate_image", ex);
        }
    }

    internal static ToolkitResult NormalizeVisionResult(string action, ToolkitResult result)
    {
        if (result.Status != ToolkitResultStatus.Ok || result.Data is not JsonElement root)
            return result;

        var text = ExtractVisionText(root);
        if (string.IsNullOrWhiteSpace(text))
            return ToolkitResult.Success(new { action, raw = root }, result.Duration);

        object normalized = action switch
        {
            "analyze_image" => new { action, analysis = text, raw = root },
            "describe" => new { action, description = text, raw = root },
            "ocr" => new { action, text, raw = root },
            "object_detect" => new { action, objects = SplitDetectedObjects(text), raw = root },
            _ => throw AgxiomeToolkitException.UnknownAction("openai_vision", action)
        };

        return ToolkitResult.Success(normalized, result.Duration);
    }

    private static string? ExtractVisionText(JsonElement root)
    {
        if (!root.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0)
            return null;

        var message = choices[0].GetProperty("message");
        if (!message.TryGetProperty("content", out var content))
            return null;

        return content.ValueKind switch
        {
            JsonValueKind.String => content.GetString(),
            JsonValueKind.Array => string.Join("\n", content.EnumerateArray()
                .Where(static item => item.TryGetProperty("type", out var type) && type.GetString() == "text")
                .Select(static item => item.TryGetProperty("text", out var textNode) ? textNode.GetString() : null)
                .Where(static text => !string.IsNullOrWhiteSpace(text))),
            _ => null
        };
    }

    private static string[] SplitDetectedObjects(string text)
        => text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static line => Regex.Replace(line, "^[\\-\\*\\d\\.\\)\\s]+", string.Empty))
            .Where(static line => !string.IsNullOrWhiteSpace(line))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    internal static async Task<ToolkitResult> OaiPost(string path,object payload,string key,CancellationToken ct){
        var req=new HttpRequestMessage(HttpMethod.Post,$"https://api.openai.com/v1/{path}"){Content=JsonContent.Create(payload)};
        req.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",key);
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
}

// T-69 — DALL-E Image Generation (api_key required)
[Toolkit("dalle","DALL-E",ToolkitCategory.AiMl)]
public sealed class DalleToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"dalle";public override string Name=>"DALL-E";
    public override ToolkitCategory Category=>ToolkitCategory.AiMl;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("api_key"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var key=GetRequiredSetting("api_key");
        return a switch{
            "generate" or "create" =>await OpenAiVisionToolkit.OaiPost("images/generations",new{model=GetParam<string>(p,"model")??"dall-e-3",prompt=GetParam<string>(p,"prompt")??"A beautiful landscape",size=GetParam<string>(p,"size")??"1024x1024",quality=GetParam<string>(p,"quality")??"standard",n=GetParam<int?>(p,"n")??1},key,ct).ConfigureAwait(false),
            "edit"                 =>await OpenAiVisionToolkit.OaiPost("images/edits",new{prompt=GetParam<string>(p,"prompt")??"",n=GetParam<int?>(p,"n")??1,size=GetParam<string>(p,"size")??"1024x1024"},key,ct).ConfigureAwait(false),
            "variation"            =>await OpenAiVisionToolkit.OaiPost("images/variations",new{n=GetParam<int?>(p,"n")??1,size=GetParam<string>(p,"size")??"1024x1024"},key,ct).ConfigureAwait(false),
            _=>throw AgxiomeToolkitException.UnknownAction(Id, a)
        };
    }
}

// T-70 — Stable Diffusion (Stability AI API — api_key required, or local endpoint)
[Toolkit("stable_diffusion","Stable Diffusion",ToolkitCategory.AiMl)]
public sealed class StableDiffusionToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"stable_diffusion";public override string Name=>"Stable Diffusion";
    public override ToolkitCategory Category=>ToolkitCategory.AiMl;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("api_key"))||!string.IsNullOrWhiteSpace(GetSetting("base_url"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var localBase=GetSetting("base_url"); // local Automatic1111/SDAPI
        if(!string.IsNullOrWhiteSpace(localBase)){
            // Local AUTOMATIC1111 API
            var resp2=await Http.Client.PostAsJsonAsync($"{localBase.TrimEnd('/')}/sdapi/v1/txt2img",new{prompt=GetParam<string>(p,"prompt"),negative_prompt=GetParam<string>(p,"negative_prompt"),steps=GetParam<int?>(p,"steps")??20,width=GetParam<int?>(p,"width")??512,height=GetParam<int?>(p,"height")??512},ct).ConfigureAwait(false);
            resp2.EnsureSuccessStatusCode();
            return ToolkitResult.Success(await resp2.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
        }
        // Stability AI cloud API
        var key=GetRequiredSetting("api_key");
        var engineId=GetParam<string>(p,"engine")??"stable-diffusion-v1-6";
        var payload=new{text_prompts=new[]{new{text=GetParam<string>(p,"prompt")??""},},cfg_scale=GetParam<int?>(p,"cfg_scale")??7,height=GetParam<int?>(p,"height")??512,width=GetParam<int?>(p,"width")??512,steps=GetParam<int?>(p,"steps")??30,samples=GetParam<int?>(p,"samples")??1};
        var req=new HttpRequestMessage(HttpMethod.Post,$"https://api.stability.ai/v1/generation/{engineId}/text-to-image"){Content=JsonContent.Create(payload)};
        req.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",key);
        req.Headers.Add("Accept","application/json");
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
}

// T-71 — Whisper (OpenAI speech-to-text — api_key required)
[Toolkit("whisper","Whisper STT",ToolkitCategory.AiMl)]
public sealed class WhisperToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"whisper";public override string Name=>"Whisper STT";
    public override ToolkitCategory Category=>ToolkitCategory.AiMl;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("api_key"))||!string.IsNullOrWhiteSpace(GetSetting("base_url"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var filePath=GetRequiredParam<string>(p,"file");
        var language=GetParam<string>(p,"language")??"";
        var prompt=GetParam<string>(p,"prompt")??"";
        var endpoint=(GetSetting("base_url")??$"https://api.openai.com").TrimEnd('/')+"/v1/audio/transcriptions";
        var key=GetSetting("api_key")??"";
        var fileBytes=await File.ReadAllBytesAsync(filePath,ct).ConfigureAwait(false);
        using var form=new MultipartFormDataContent();
        form.Add(new ByteArrayContent(fileBytes),"file",Path.GetFileName(filePath));
        form.Add(new StringContent(GetParam<string>(p,"model")??"whisper-1"),"model");
        if(!string.IsNullOrWhiteSpace(language))form.Add(new StringContent(language),"language");
        if(!string.IsNullOrWhiteSpace(prompt))form.Add(new StringContent(prompt),"prompt");
        form.Add(new StringContent(GetParam<string>(p,"response_format")??"json"),"response_format");
        var req=new HttpRequestMessage(HttpMethod.Post,endpoint){Content=form};
        if(!string.IsNullOrWhiteSpace(key))req.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",key);
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
}

// T-72 — ElevenLabs TTS (api_key required)
[Toolkit("elevenlabs","ElevenLabs TTS",ToolkitCategory.AiMl)]
public sealed class ElevenLabsToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"elevenlabs";public override string Name=>"ElevenLabs TTS";
    public override ToolkitCategory Category=>ToolkitCategory.AiMl;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("api_key"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var key=GetRequiredSetting("api_key");
        return a switch{
            "list_voices" =>await ElGet("voices",key,ct).ConfigureAwait(false),
            "get_models"  =>await ElGet("models",key,ct).ConfigureAwait(false),
            "synthesize" or "tts" =>await ElSynthesize(p,key,ct).ConfigureAwait(false),
            _=>throw AgxiomeToolkitException.UnknownAction(Id, a)
        };
    }
    private static async Task<ToolkitResult> ElSynthesize(Dictionary<string,object?> p,string key,CancellationToken ct){
        var voiceId=p.TryGetValue("voice_id",out var v)?v?.ToString()??"21m00Tcm4TlvDq8ikWAM":"21m00Tcm4TlvDq8ikWAM";
        var text=p.TryGetValue("text",out var t)?t?.ToString()??"":"";
        var model=p.TryGetValue("model_id",out var m)?m?.ToString()??"eleven_monolingual_v1":"eleven_monolingual_v1";
        var payload=new{text,model_id=model,voice_settings=new{stability=0.5,similarity_boost=0.5}};
        var req=new HttpRequestMessage(HttpMethod.Post,$"https://api.elevenlabs.io/v1/text-to-speech/{voiceId}"){Content=JsonContent.Create(payload)};
        req.Headers.Add("xi-api-key",key);
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var audioBytes=await resp.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
        // save to temp file
        var outPath=p.TryGetValue("output_file",out var o)?o?.ToString()??"":Path.Combine(Path.GetTempPath(),$"el_{Guid.NewGuid():N}.mp3");
        await File.WriteAllBytesAsync(outPath,audioBytes,ct).ConfigureAwait(false);
        return ToolkitResult.Success(new{outputFile=outPath,sizeBytes=audioBytes.Length,voiceId,model});
    }
    private static async Task<ToolkitResult> ElGet(string path,string key,CancellationToken ct){
        var req=new HttpRequestMessage(HttpMethod.Get,$"https://api.elevenlabs.io/v1/{path}");
        req.Headers.Add("xi-api-key",key);
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
}

// T-73 — Hugging Face Inference API (api_key optional for public models)
[Toolkit("huggingface","Hugging Face",ToolkitCategory.AiMl)]
public sealed class HuggingFaceHubToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"huggingface";public override string Name=>"Hugging Face";
    public override ToolkitCategory Category=>ToolkitCategory.AiMl;
    public override bool IsConfigured=>true;
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var key=GetSetting("api_key");
        var model=GetParam<string>(p,"model")??"gpt2";
        var inputs=GetParam<object?>(p,"inputs")??GetParam<string>(p,"text")??"";
        var parameters=GetParam<object?>(p,"parameters");
        object payload=parameters!=null?new{inputs,parameters}:new{inputs};
        var req=new HttpRequestMessage(HttpMethod.Post,$"https://api-inference.huggingface.co/models/{model}"){Content=JsonContent.Create(payload)};
        if(!string.IsNullOrWhiteSpace(key))req.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",key);
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);
        if(resp.StatusCode==System.Net.HttpStatusCode.ServiceUnavailable){
            return ToolkitResult.Fail($"Model {model} is loading. Retry in 20 seconds.");
        }
        resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
}

// T-74 — ML.NET AutoML (BCL — no extra packages, works with Microsoft.ML)
[Toolkit("mlnet","ML.NET AutoML",ToolkitCategory.AiMl)]
public sealed class MlNetAutoMlToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"mlnet";public override string Name=>"ML.NET AutoML";
    public override ToolkitCategory Category=>ToolkitCategory.AiMl;
    public override bool IsConfigured=>true;
    protected override Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct)
        =>Task.FromResult(ToolkitResult.Fail("ML.NET AutoML requires package Microsoft.ML and Microsoft.ML.AutoML. Add them and use MLContext for training (binary/multiclass/regression). No HTTP calls needed — all local computation."));
}

// T-75 — PyTorch (requires Python + PyTorch — run via PythonRunnerToolkit or REST)
[Toolkit("pytorch","PyTorch",ToolkitCategory.AiMl)]
public sealed class PyTorchToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"pytorch";public override string Name=>"PyTorch";
    public override ToolkitCategory Category=>ToolkitCategory.AiMl;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("endpoint"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var endpoint=GetRequiredSetting("endpoint");
        var payload=GetParam<object?>(p,"inputs")??GetParam<object?>(p,"data")??new{};
        var resp=await Http.Client.PostAsJsonAsync(endpoint,payload,ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
}

// T-76 — ONNX Runtime (local inference — requires Microsoft.ML.OnnxRuntime)
[Toolkit("onnx","ONNX Runtime",ToolkitCategory.AiMl)]
public sealed class OnnxToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"onnx";public override string Name=>"ONNX Runtime";
    public override ToolkitCategory Category=>ToolkitCategory.AiMl;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("model_path"));
    protected override Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct)
        =>Task.FromResult(ToolkitResult.Fail("ONNX inference requires package Microsoft.ML.OnnxRuntime. Add it and use InferenceSession to load model from model_path setting, then run inference with input tensors."));
}

// T-77 — LangChain Bridge (call LangChain-powered Python/Node endpoint)
[Toolkit("langchain","LangChain Bridge",ToolkitCategory.AiMl)]
public sealed class LangChainBridgeToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"langchain";public override string Name=>"LangChain Bridge";
    public override ToolkitCategory Category=>ToolkitCategory.AiMl;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("endpoint"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var endpoint=GetRequiredSetting("endpoint");
        var payload=new{input=GetParam<object?>(p,"input")??GetParam<string>(p,"query"),config=GetParam<object?>(p,"config")};
        var req=new HttpRequestMessage(HttpMethod.Post,$"{endpoint.TrimEnd('/')}/invoke"){Content=JsonContent.Create(payload)};
        var apiKey=GetSetting("api_key");
        if(!string.IsNullOrWhiteSpace(apiKey))req.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",apiKey);
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
}

// T-78 — Semantic Kernel Bridge (call SK-powered .NET endpoint or invoke directly)
[Toolkit("semantic_kernel","Semantic Kernel",ToolkitCategory.AiMl)]
public sealed class SemanticKernelToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"semantic_kernel";public override string Name=>"Semantic Kernel";
    public override ToolkitCategory Category=>ToolkitCategory.AiMl;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("endpoint"))||!string.IsNullOrWhiteSpace(GetSetting("api_key"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var endpoint=GetSetting("endpoint");
        if(!string.IsNullOrWhiteSpace(endpoint)){
            // REST bridge mode
            var payload=new{action=a,parameters=p};
            var resp=await Http.Client.PostAsJsonAsync($"{endpoint.TrimEnd('/')}/invoke",payload,ct).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
            return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
        }
        // Direct OpenAI call through SK-style chat
        return await OpenAiVisionToolkit.OaiPost("chat/completions",new{
            model=GetParam<string>(p,"model")??"gpt-4o-mini",
            messages=new[]{new{role="system",content=GetParam<string>(p,"system_prompt")??"You are a helpful assistant."},new{role="user",content=GetParam<string>(p,"prompt")??GetParam<string>(p,"input")??""}}
        },GetRequiredSetting("api_key"),ct).ConfigureAwait(false);
    }
}

// T-79 — ML.NET Scikit-like (informative stub)
[Toolkit("mlnet_scikit","ML.NET Scikit-like",ToolkitCategory.AiMl)]
public sealed class MlNetSkikitToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"mlnet_scikit";public override string Name=>"ML.NET Scikit-like";
    public override ToolkitCategory Category=>ToolkitCategory.AiMl;
    public override bool IsConfigured=>true;
    protected override Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct)
        =>Task.FromResult(ToolkitResult.Fail("ML.NET classification/regression/clustering: add packages Microsoft.ML, Microsoft.ML.FastTree, Microsoft.ML.LightGbm. Use MLContext.Regression/Classification/Clustering trainers. No HTTP calls — fully local."));
}

