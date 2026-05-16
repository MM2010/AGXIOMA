// T-90..T-101 — Cloud & Infrastructure Toolkits
// Most cloud vendor SDKs are heavy. These toolkits delegate to their respective CLI tools
// (aws, az, gcloud) when available, or provide informative stubs for SDK-only operations.
using System.Net.Http.Json;
using System.Text.Json;
using AGXIOME.Shared.Errors;

namespace AGXIOME.Toolkits.CloudInfra;

// ── shared HTTP client ─────────────────────────────────────────────────────
file static class Http { internal static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(30) }; }

// T-90 — AWS S3 (delegates to aws CLI — or use AWSSDK.S3)
[Toolkit("aws_s3","AWS S3",ToolkitCategory.CloudInfra)]
public sealed class AwsS3Toolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"aws_s3";public override string Name=>"AWS S3";
    public override ToolkitCategory Category=>ToolkitCategory.CloudInfra;
    public override bool IsConfigured=>true;
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var bucket=GetParam<string>(p,"bucket")??"";
        var key=GetParam<string>(p,"key")??"";
        var args=a switch{
            "list_buckets"  =>"s3 ls --output json",
            "list_objects"  =>$"s3 ls s3://{bucket}/ --recursive",
            "upload"        =>$"s3 cp {GetParam<string>(p,"local_path")} s3://{bucket}/{key}",
            "download"      =>$"s3 cp s3://{bucket}/{key} {GetParam<string>(p,"local_path")??"./"}",
            "delete"        =>$"s3 rm s3://{bucket}/{key}",
            "presign"       =>$"s3 presign s3://{bucket}/{key} --expires-in {GetParam<int?>(p,"expires_in")??3600}",
            _=>$"s3 {GetParam<string>(p,"args")??a}"
        };
        return await RunAwsCli(args,ct).ConfigureAwait(false);
    }
    private async Task<ToolkitResult> RunAwsCli(string args,CancellationToken ct){
        var psi=new System.Diagnostics.ProcessStartInfo("aws",args){RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false};
        if(!string.IsNullOrWhiteSpace(GetSetting("access_key_id")))psi.Environment["AWS_ACCESS_KEY_ID"]=GetSetting("access_key_id");
        if(!string.IsNullOrWhiteSpace(GetSetting("secret_access_key")))psi.Environment["AWS_SECRET_ACCESS_KEY"]=GetSetting("secret_access_key");
        if(!string.IsNullOrWhiteSpace(GetSetting("region")))psi.Environment["AWS_DEFAULT_REGION"]=GetSetting("region");
        using var proc=System.Diagnostics.Process.Start(psi)??throw AgxiomeToolkitException.DependencyUnavailable(Id, "aws");
        var out_=await proc.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
        var err=await proc.StandardError.ReadToEndAsync(ct).ConfigureAwait(false);
        await proc.WaitForExitAsync(ct).ConfigureAwait(false);
        return proc.ExitCode==0?ToolkitResult.Success(new{command=$"aws {args}",output=out_.Trim()}):ToolkitResult.Fail($"aws CLI failed: {err.Trim()}");
    }
}

// T-91 — AWS Lambda (aws CLI)
[Toolkit("aws_lambda","AWS Lambda",ToolkitCategory.CloudInfra)]
public sealed class AwsLambdaToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"aws_lambda";public override string Name=>"AWS Lambda";
    public override ToolkitCategory Category=>ToolkitCategory.CloudInfra;
    public override bool IsConfigured=>true;
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var fn=GetRequiredParam<string>(p,"function_name");
        var args=a switch{
            "invoke"        =>$"lambda invoke --function-name {fn} --payload {System.Text.Json.JsonSerializer.Serialize(GetParam<object?>(p,"payload")??new{})} /tmp/lambda-out.json --output json && cat /tmp/lambda-out.json",
            "list"          =>"lambda list-functions --output json",
            "get"           =>$"lambda get-function --function-name {fn} --output json",
            "list_versions" =>$"lambda list-versions-by-function --function-name {fn} --output json",
            _               =>$"lambda {GetParam<string>(p,"args")??a}"
        };
        return await RunAwsCli(args,ct).ConfigureAwait(false);
    }
    private async Task<ToolkitResult> RunAwsCli(string args,CancellationToken ct){
        var psi=new System.Diagnostics.ProcessStartInfo("aws",args){RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false};
        if(!string.IsNullOrWhiteSpace(GetSetting("access_key_id")))psi.Environment["AWS_ACCESS_KEY_ID"]=GetSetting("access_key_id");
        if(!string.IsNullOrWhiteSpace(GetSetting("secret_access_key")))psi.Environment["AWS_SECRET_ACCESS_KEY"]=GetSetting("secret_access_key");
        if(!string.IsNullOrWhiteSpace(GetSetting("region")))psi.Environment["AWS_DEFAULT_REGION"]=GetSetting("region");
        using var proc=System.Diagnostics.Process.Start(psi)??throw AgxiomeToolkitException.DependencyUnavailable(Id, "aws");
        var out_=await proc.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
        var err=await proc.StandardError.ReadToEndAsync(ct).ConfigureAwait(false);
        await proc.WaitForExitAsync(ct).ConfigureAwait(false);
        return proc.ExitCode==0?ToolkitResult.Success(new{command=$"aws {args}",output=out_.Trim()}):ToolkitResult.Fail($"aws CLI: {err.Trim()}");
    }
}

// T-92 — AWS DynamoDB (aws CLI)
[Toolkit("aws_dynamodb","AWS DynamoDB",ToolkitCategory.CloudInfra)]
public sealed class AwsDynamoDbToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"aws_dynamodb";public override string Name=>"AWS DynamoDB";
    public override ToolkitCategory Category=>ToolkitCategory.CloudInfra;
    public override bool IsConfigured=>true;
    protected override Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct)
        =>Task.FromResult(ToolkitResult.Fail("DynamoDB: use aws CLI (table: list-tables, get-item, put-item, query) or add package AWSSDK.DynamoDBv2. Settings: access_key_id, secret_access_key, region."));
}

// T-93 — AWS EC2 (aws CLI)
[Toolkit("aws_ec2","AWS EC2",ToolkitCategory.CloudInfra)]
public sealed class AwsEc2Toolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"aws_ec2";public override string Name=>"AWS EC2";
    public override ToolkitCategory Category=>ToolkitCategory.CloudInfra;
    public override bool IsConfigured=>true;
    protected override Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct)
        =>Task.FromResult(ToolkitResult.Fail("EC2: use aws CLI (ec2 describe-instances, start-instances, stop-instances, terminate-instances) or add package AWSSDK.EC2. Settings: access_key_id, secret_access_key, region."));
}

// T-94 — Azure Blob Storage (az CLI or Azure.Storage.Blobs)
[Toolkit("azure_blob","Azure Blob",ToolkitCategory.CloudInfra)]
public sealed class AzureBlobToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"azure_blob";public override string Name=>"Azure Blob";
    public override ToolkitCategory Category=>ToolkitCategory.CloudInfra;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("connection_string"))||!string.IsNullOrWhiteSpace(GetSetting("account_name"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var conn=GetSetting("connection_string");
        var account=GetSetting("account_name");
        var container=GetParam<string>(p,"container")??"";
        var blob=GetParam<string>(p,"blob")??"";
        // Try az CLI
        var connArg=conn!=null?$"--connection-string \"{conn}\"":account!=null?$"--account-name {account}":"";
        var args=a switch{
            "list_containers"=>$"storage container list {connArg} --output json",
            "list_blobs"     =>$"storage blob list --container-name {container} {connArg} --output json",
            "upload"         =>$"storage blob upload --container-name {container} --name {blob} --file {GetRequiredParam<string>(p,"file")} {connArg}",
            "download"       =>$"storage blob download --container-name {container} --name {blob} --file {GetRequiredParam<string>(p,"dest")} {connArg}",
            "delete"         =>$"storage blob delete --container-name {container} --name {blob} {connArg}",
            _                =>$"storage blob {GetParam<string>(p,"args")??a} {connArg}"
        };
        var psi=new System.Diagnostics.ProcessStartInfo("az",args){RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false};
        using var proc=System.Diagnostics.Process.Start(psi)??throw AgxiomeToolkitException.DependencyUnavailable(Id, "az");
        var out_=await proc.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
        var err=await proc.StandardError.ReadToEndAsync(ct).ConfigureAwait(false);
        await proc.WaitForExitAsync(ct).ConfigureAwait(false);
        return proc.ExitCode==0?ToolkitResult.Success(new{command=$"az {args}",output=out_.Trim()}):ToolkitResult.Fail($"az: {err.Trim()}");
    }
}

// T-95 — Azure Functions (az CLI)
[Toolkit("azure_functions","Azure Functions",ToolkitCategory.CloudInfra)]
public sealed class AzureFunctionsToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"azure_functions";public override string Name=>"Azure Functions";
    public override ToolkitCategory Category=>ToolkitCategory.CloudInfra;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("function_app"))||!string.IsNullOrWhiteSpace(GetSetting("function_url"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var fnUrl=GetSetting("function_url");
        if(!string.IsNullOrWhiteSpace(fnUrl)){
            // HTTP trigger invoke
            var payload=GetParam<object?>(p,"payload")??new{};
            var resp=await Http.Client.PostAsJsonAsync(fnUrl,payload,ct).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
            var body=await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            object result;try{result=JsonDocument.Parse(body).RootElement;}catch{result=body;}
            return ToolkitResult.Success(new{url=fnUrl,statusCode=(int)resp.StatusCode,result});
        }
        return ToolkitResult.Fail("Set function_url for HTTP trigger invocation, or use azure_cli toolkit for management (az functionapp ...). Package: Azure.ResourceManager.AppService.");
    }
}

// T-96 — GCP Storage (gcloud CLI)
[Toolkit("gcp_storage","GCP Storage",ToolkitCategory.CloudInfra)]
public sealed class GcpStorageToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"gcp_storage";public override string Name=>"GCP Storage";
    public override ToolkitCategory Category=>ToolkitCategory.CloudInfra;
    public override bool IsConfigured=>true;
    protected override Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct)
        =>Task.FromResult(ToolkitResult.Fail("GCP Storage: use gcp_cli toolkit (gcloud storage ls|cp|rm|cat) or add package Google.Cloud.Storage.V1. Settings: project_id, service_account_key_json."));
}

// T-97 — GCP Cloud Run (gcloud CLI)
[Toolkit("gcp_cloudrun","GCP Cloud Run",ToolkitCategory.CloudInfra)]
public sealed class GcpCloudRunToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"gcp_cloudrun";public override string Name=>"GCP Cloud Run";
    public override ToolkitCategory Category=>ToolkitCategory.CloudInfra;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("service_url"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var url=GetRequiredSetting("service_url");
        var payload=GetParam<object?>(p,"payload")??new{};
        var method=GetParam<string>(p,"method")??"POST";
        HttpResponseMessage resp;
        if(method.Equals("GET",StringComparison.OrdinalIgnoreCase))resp=await Http.Client.GetAsync(url,ct).ConfigureAwait(false);
        else resp=await Http.Client.PostAsJsonAsync(url,payload,ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var body=await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        object result;try{result=JsonDocument.Parse(body).RootElement;}catch{result=body;}
        return ToolkitResult.Success(new{url,statusCode=(int)resp.StatusCode,result});
    }
}

// T-98 — Cloudflare (API token required)
[Toolkit("cloudflare","Cloudflare",ToolkitCategory.CloudInfra)]
public sealed class CloudflareToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"cloudflare";public override string Name=>"Cloudflare";
    public override ToolkitCategory Category=>ToolkitCategory.CloudInfra;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("api_token"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var token=GetRequiredSetting("api_token");
        var zoneId=GetSetting("zone_id")??GetParam<string>(p,"zone_id")??"";
        return a switch{
            "list_zones"   =>await CfGet("zones",token,ct).ConfigureAwait(false),
            "list_dns"     =>await CfGet($"zones/{zoneId}/dns_records",token,ct).ConfigureAwait(false),
            "purge_cache"  =>await CfPost($"zones/{zoneId}/purge_cache",new{purge_everything=true},token,ct).ConfigureAwait(false),
            "list_workers" =>await CfGet("accounts/{GetSetting(\"account_id\")}/workers/scripts",token,ct).ConfigureAwait(false),
            "analytics"    =>await CfGet($"zones/{zoneId}/analytics/dashboard",token,ct).ConfigureAwait(false),
            _=>throw AgxiomeToolkitException.UnknownAction(Id, a)
        };
    }
    private static async Task<ToolkitResult> CfGet(string path,string token,CancellationToken ct){
        var req=new HttpRequestMessage(HttpMethod.Get,$"https://api.cloudflare.com/client/v4/{path}");
        req.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",token);
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
    private static async Task<ToolkitResult> CfPost(string path,object body,string token,CancellationToken ct){
        var req=new HttpRequestMessage(HttpMethod.Post,$"https://api.cloudflare.com/client/v4/{path}"){Content=JsonContent.Create(body)};
        req.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",token);
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
}

// T-99 — Vercel (api_token required)
[Toolkit("vercel","Vercel",ToolkitCategory.CloudInfra)]
public sealed class VercelToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"vercel";public override string Name=>"Vercel";
    public override ToolkitCategory Category=>ToolkitCategory.CloudInfra;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("api_token"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var token=GetRequiredSetting("api_token");
        var teamId=GetSetting("team_id");
        var q=teamId!=null?$"?teamId={teamId}":"";
        return a switch{
            "list_deployments"=>await VcGet($"v6/deployments{q}&limit={GetParam<int?>(p,"limit")??10}",token,ct).ConfigureAwait(false),
            "get_deployment"  =>await VcGet($"v13/deployments/{GetParam<string>(p,"id")}",token,ct).ConfigureAwait(false),
            "list_projects"   =>await VcGet($"v9/projects{q}",token,ct).ConfigureAwait(false),
            "get_domains"     =>await VcGet($"v5/domains{q}",token,ct).ConfigureAwait(false),
            _=>throw AgxiomeToolkitException.UnknownAction(Id, a)
        };
    }
    private static async Task<ToolkitResult> VcGet(string path,string token,CancellationToken ct){
        var req=new HttpRequestMessage(HttpMethod.Get,$"https://api.vercel.com/{path}");
        req.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",token);
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
}

// T-100 — Netlify (access_token required)
[Toolkit("netlify","Netlify",ToolkitCategory.CloudInfra)]
public sealed class NetlifyToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"netlify";public override string Name=>"Netlify";
    public override ToolkitCategory Category=>ToolkitCategory.CloudInfra;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("access_token"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var token=GetRequiredSetting("access_token");
        return a switch{
            "list_sites"  =>await NtGet("sites",token,ct).ConfigureAwait(false),
            "get_site"    =>await NtGet($"sites/{GetParam<string>(p,"site_id")}",token,ct).ConfigureAwait(false),
            "list_deploys"=>await NtGet($"sites/{GetParam<string>(p,"site_id")}/deploys?per_page={GetParam<int?>(p,"per_page")??10}",token,ct).ConfigureAwait(false),
            "get_deploy"  =>await NtGet($"deploys/{GetParam<string>(p,"deploy_id")}",token,ct).ConfigureAwait(false),
            _=>throw AgxiomeToolkitException.UnknownAction(Id, a)
        };
    }
    private static async Task<ToolkitResult> NtGet(string path,string token,CancellationToken ct){
        var req=new HttpRequestMessage(HttpMethod.Get,$"https://api.netlify.com/api/v1/{path}");
        req.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",token);
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
}

// T-101 — DigitalOcean (api_token required)
[Toolkit("digitalocean","DigitalOcean",ToolkitCategory.CloudInfra)]
public sealed class DigitalOceanToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"digitalocean";public override string Name=>"DigitalOcean";
    public override ToolkitCategory Category=>ToolkitCategory.CloudInfra;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("api_token"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var token=GetRequiredSetting("api_token");
        return a switch{
            "list_droplets"   =>await DoGet("droplets",token,ct).ConfigureAwait(false),
            "get_droplet"     =>await DoGet($"droplets/{GetParam<string>(p,"id")}",token,ct).ConfigureAwait(false),
            "list_domains"    =>await DoGet("domains",token,ct).ConfigureAwait(false),
            "list_k8s"        =>await DoGet("kubernetes/clusters",token,ct).ConfigureAwait(false),
            "list_apps"       =>await DoGet("apps",token,ct).ConfigureAwait(false),
            "account"         =>await DoGet("account",token,ct).ConfigureAwait(false),
            _=>throw AgxiomeToolkitException.UnknownAction(Id, a)
        };
    }
    private static async Task<ToolkitResult> DoGet(string path,string token,CancellationToken ct){
        var req=new HttpRequestMessage(HttpMethod.Get,$"https://api.digitalocean.com/v2/{path}");
        req.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",token);
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
}
