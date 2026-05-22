// T-48..T-67 — Development Toolkits
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AGXIOME.Shared.Errors;

namespace AGXIOME.Toolkits.Development;

// ── shared HTTP client ─────────────────────────────────────────────────────
file static class Http
{
    internal static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(30) };
}

// T-48 — GitHub REST API (token required for write ops; public repos work without)
[Toolkit("github","GitHub",ToolkitCategory.Development)]
public sealed class GitHubToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"github";public override string Name=>"GitHub";
    public override ToolkitCategory Category=>ToolkitCategory.Development;
    public override bool IsConfigured=>true; // public API works without token
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var repo=GetParam<string>(p,"repo")??""; // owner/repo
        return a switch{
            "get_repo"          =>await GhGet($"repos/{repo}",ct).ConfigureAwait(false),
            "list_issues"       =>await GhGet($"repos/{repo}/issues?state={GetParam<string>(p,"state")??"open"}&per_page={GetParam<int?>(p,"per_page")??30}",ct).ConfigureAwait(false),
            "get_issue"         =>await GhGet($"repos/{repo}/issues/{GetParam<string>(p,"number")}",ct).ConfigureAwait(false),
            "create_issue"      =>await GhPost($"repos/{repo}/issues",new{title=GetParam<string>(p,"title"),body=GetParam<string>(p,"body"),labels=GetParam<string[]?>(p,"labels")},ct).ConfigureAwait(false),
            "list_prs"          =>await GhGet($"repos/{repo}/pulls?state={GetParam<string>(p,"state")??"open"}&per_page={GetParam<int?>(p,"per_page")??30}",ct).ConfigureAwait(false),
            "get_pr"            =>await GhGet($"repos/{repo}/pulls/{GetParam<string>(p,"number")}",ct).ConfigureAwait(false),
            "list_commits"      =>await GhGet($"repos/{repo}/commits?per_page={GetParam<int?>(p,"per_page")??30}&sha={GetParam<string>(p,"sha")??"HEAD"}",ct).ConfigureAwait(false),
            "get_file"          =>await GhGet($"repos/{repo}/contents/{GetParam<string>(p,"path")}",ct).ConfigureAwait(false),
            "search_code"       =>await GhGet($"search/code?q={Uri.EscapeDataString(GetParam<string>(p,"q")??"")}+repo:{repo}&per_page={GetParam<int?>(p,"per_page")??10}",ct).ConfigureAwait(false),
            "list_releases"     =>await GhGet($"repos/{repo}/releases",ct).ConfigureAwait(false),
            "list_workflows"    =>await GhGet($"repos/{repo}/actions/workflows",ct).ConfigureAwait(false),
            "trigger_workflow"  =>await GhPost($"repos/{repo}/actions/workflows/{GetParam<string>(p,"workflow_id")}/dispatches",new{@ref=GetParam<string>(p,"ref")??"main"},ct).ConfigureAwait(false),
            "list_branches"     =>await GhGet($"repos/{repo}/branches",ct).ConfigureAwait(false),
            _=>throw AgxiomeToolkitException.UnknownAction(Id, a)
        };
    }
    private async Task<ToolkitResult> GhGet(string path,CancellationToken ct){
        var req=new HttpRequestMessage(HttpMethod.Get,$"https://api.github.com/{path}");
        req.Headers.Add("User-Agent","AGXIOMA-Agent/1.0");
        req.Headers.Add("Accept","application/vnd.github+json");
        var token=GetSetting("token")??GetSetting("api_key");
        if(!string.IsNullOrWhiteSpace(token))req.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",token);
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
    private async Task<ToolkitResult> GhPost(string path,object body,CancellationToken ct){
        var req=new HttpRequestMessage(HttpMethod.Post,$"https://api.github.com/{path}"){Content=JsonContent.Create(body)};
        req.Headers.Add("User-Agent","AGXIOMA-Agent/1.0");
        req.Headers.Add("Accept","application/vnd.github+json");
        var token=GetSetting("token")??GetSetting("api_key")??throw AgxiomeToolkitException.MissingSetting(Id, "token");
        req.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",token);
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var content=await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        return ToolkitResult.Success(string.IsNullOrWhiteSpace(content)?new{success=true}:(object)JsonDocument.Parse(content).RootElement);
    }
}

// T-49 — GitLab (private_token or oauth_token)
[Toolkit("gitlab","GitLab",ToolkitCategory.Development)]
public sealed class GitLabToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"gitlab";public override string Name=>"GitLab";
    public override ToolkitCategory Category=>ToolkitCategory.Development;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("token"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var token=GetRequiredSetting("token");
        var base_=(GetSetting("base_url")??"https://gitlab.com").TrimEnd('/');
        var projectId=Uri.EscapeDataString(GetParam<string>(p,"project")??"");
        return a switch{
            "list_projects" =>await GlGet($"{base_}/api/v4/projects?membership=true",token,ct).ConfigureAwait(false),
            "get_project"   =>await GlGet($"{base_}/api/v4/projects/{projectId}",token,ct).ConfigureAwait(false),
            "list_issues"   =>await GlGet($"{base_}/api/v4/projects/{projectId}/issues?state={GetParam<string>(p,"state")??"opened"}",token,ct).ConfigureAwait(false),
            "list_mrs"      =>await GlGet($"{base_}/api/v4/projects/{projectId}/merge_requests?state={GetParam<string>(p,"state")??"opened"}",token,ct).ConfigureAwait(false),
            "list_commits"  =>await GlGet($"{base_}/api/v4/projects/{projectId}/repository/commits",token,ct).ConfigureAwait(false),
            _=>throw AgxiomeToolkitException.UnknownAction(Id, a)
        };
    }
    private static async Task<ToolkitResult> GlGet(string url,string token,CancellationToken ct){
        var req=new HttpRequestMessage(HttpMethod.Get,url);
        req.Headers.Add("PRIVATE-TOKEN",token);
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
}

// T-50 — Bitbucket (username + app_password)
[Toolkit("bitbucket","Bitbucket",ToolkitCategory.Development)]
public sealed class BitbucketToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"bitbucket";public override string Name=>"Bitbucket";
    public override ToolkitCategory Category=>ToolkitCategory.Development;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("username"))&&!string.IsNullOrWhiteSpace(GetSetting("app_password"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var user=GetRequiredSetting("username");
        var pass=GetRequiredSetting("app_password");
        var creds=Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{pass}"));
        var ws=GetParam<string>(p,"workspace")??user;
        var repo=GetParam<string>(p,"repo")??"";
        return a switch{
            "list_repos"   =>await BbGet($"repositories/{ws}",creds,ct).ConfigureAwait(false),
            "get_repo"     =>await BbGet($"repositories/{ws}/{repo}",creds,ct).ConfigureAwait(false),
            "list_prs"     =>await BbGet($"repositories/{ws}/{repo}/pullrequests?state={GetParam<string>(p,"state")??"OPEN"}",creds,ct).ConfigureAwait(false),
            "list_commits" =>await BbGet($"repositories/{ws}/{repo}/commits",creds,ct).ConfigureAwait(false),
            _=>throw AgxiomeToolkitException.UnknownAction(Id, a)
        };
    }
    private static async Task<ToolkitResult> BbGet(string path,string creds,CancellationToken ct){
        var req=new HttpRequestMessage(HttpMethod.Get,$"https://api.bitbucket.org/2.0/{path}");
        req.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Basic",creds);
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
}

// T-51 — Git CLI wrapper (BCL Process)
[Toolkit("git","Git",ToolkitCategory.Development)]
public sealed class GitToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"git";public override string Name=>"Git";
    public override ToolkitCategory Category=>ToolkitCategory.Development;
    public override bool IsConfigured=>true;
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var workDir=GetParam<string>(p,"working_dir")??GetSetting("working_dir")??Directory.GetCurrentDirectory();
        return a switch{
            "status"  =>await RunGit(workDir,"status --short",ct).ConfigureAwait(false),
            "log"     =>await RunGit(workDir,$"log --oneline -n {GetParam<int?>(p,"n")??20}",ct).ConfigureAwait(false),
            "diff"    =>await RunGit(workDir,$"diff {GetParam<string>(p,"args")??""}",ct).ConfigureAwait(false),
            "branch"  =>await RunGit(workDir,"branch -a",ct).ConfigureAwait(false),
            "stash"   =>await RunGit(workDir,$"stash {GetParam<string>(p,"command")??"list"}",ct).ConfigureAwait(false),
            "pull"    =>await RunGit(workDir,"pull",ct).ConfigureAwait(false),
            "fetch"   =>await RunGit(workDir,"fetch --all",ct).ConfigureAwait(false),
            "add"     =>await RunGit(workDir,$"add {GetParam<string>(p,"path")??"."} ",ct).ConfigureAwait(false),
            "commit"  =>await RunGit(workDir,$"commit -m \"{GetParam<string>(p,"message")??"chore: auto-commit"}\"",ct).ConfigureAwait(false),
            "push"    =>await RunGit(workDir,"push",ct).ConfigureAwait(false),
            "clone"   =>await RunGit(workDir,$"clone {GetRequiredParam<string>(p,"url")} {GetParam<string>(p,"dir")??""}",ct).ConfigureAwait(false),
            "checkout"=>await RunGit(workDir,$"checkout {GetRequiredParam<string>(p,"branch")}",ct).ConfigureAwait(false),
            _=>await RunGit(workDir,GetParam<string>(p,"args")??a,ct).ConfigureAwait(false) // raw git command
        };
    }
    private static async Task<ToolkitResult> RunGit(string workDir,string args,CancellationToken ct){
        var psi=new ProcessStartInfo("git",args){WorkingDirectory=workDir,RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false};
        using var proc=Process.Start(psi)??throw AgxiomeToolkitException.DependencyUnavailable("git", "git");
        var stdout=await proc.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
        var stderr=await proc.StandardError.ReadToEndAsync(ct).ConfigureAwait(false);
        await proc.WaitForExitAsync(ct).ConfigureAwait(false);
        return proc.ExitCode==0
            ?ToolkitResult.Success(new{command=$"git {args}",stdout=stdout.Trim(),exitCode=0})
            :ToolkitResult.Fail($"git {args} failed (exit {proc.ExitCode}): {stderr.Trim()}");
    }
}

// T-52 — NuGet search API (no auth needed)
[Toolkit("nuget","NuGet",ToolkitCategory.Development)]
public sealed class NuGetToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"nuget";public override string Name=>"NuGet";
    public override ToolkitCategory Category=>ToolkitCategory.Development;
    public override bool IsConfigured=>true;
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        return a switch{
            "search"       =>await NuGetSearch(GetParam<string>(p,"query")??"",GetParam<int?>(p,"take")??20,ct).ConfigureAwait(false),
            "get_versions" =>await NuGetVersions(GetParam<string>(p,"package")??"",ct).ConfigureAwait(false),
            "get_info"     =>await NuGetInfo(GetParam<string>(p,"package")??"",GetParam<string>(p,"version")??"",ct).ConfigureAwait(false),
            "check_exists" =>await CheckExists(GetParam<string>(p,"package")??"",ct).ConfigureAwait(false),
            "latest_stable"=>await LatestStable(GetParam<string>(p,"package")??"",ct).ConfigureAwait(false),
            _=>throw AgxiomeToolkitException.UnknownAction(Id, a)
        };
    }
    private static async Task<ToolkitResult> NuGetSearch(string q,int take,CancellationToken ct){
        var json=await Http.Client.GetFromJsonAsync<JsonElement>($"https://azuresearch-usnc.nuget.org/query?q={Uri.EscapeDataString(q)}&take={take}&semVerLevel=2.0.0",ct).ConfigureAwait(false);
        var data=json.TryGetProperty("data",out var d)?d.EnumerateArray().Select(p=>new{
            id=p.TryGetProperty("id",out var i)?i.GetString():"",
            version=p.TryGetProperty("version",out var v)?v.GetString():"",
            description=p.TryGetProperty("description",out var desc)?desc.GetString():"",
            downloads=p.TryGetProperty("totalDownloads",out var dl)?dl.GetInt64():0}).ToArray():[];
        return ToolkitResult.Success(new{query=q,packages=data,count=data.Length});
    }
    private static async Task<ToolkitResult> NuGetVersions(string pkg,CancellationToken ct){
        var json=await Http.Client.GetFromJsonAsync<JsonElement>($"https://api.nuget.org/v3-flatcontainer/{pkg.ToLowerInvariant()}/index.json",ct).ConfigureAwait(false);
        var versions=json.TryGetProperty("versions",out var v)?v.EnumerateArray().Select(x=>x.GetString()??"").ToArray():[];
        return ToolkitResult.Success(new{package=pkg,versions,latest=versions.LastOrDefault()});
    }
    private static async Task<ToolkitResult> NuGetInfo(string pkg,string version,CancellationToken ct){
        if(string.IsNullOrWhiteSpace(version)){
            var idx=await Http.Client.GetFromJsonAsync<JsonElement>($"https://api.nuget.org/v3-flatcontainer/{pkg.ToLowerInvariant()}/index.json",ct).ConfigureAwait(false);
            version=idx.TryGetProperty("versions",out var vs)?vs.EnumerateArray().Select(x=>x.GetString()??"").LastOrDefault()??"":"";
        }
        var json=await Http.Client.GetFromJsonAsync<JsonElement>($"https://api.nuget.org/v3/registration5-semver1/{pkg.ToLowerInvariant()}/{version}.json",ct).ConfigureAwait(false);
        return ToolkitResult.Success(new{package=pkg,version,info=json});
    }
    private static async Task<ToolkitResult> CheckExists(string pkg,CancellationToken ct){
        try{
            var resp=await Http.Client.GetAsync($"https://api.nuget.org/v3-flatcontainer/{pkg.ToLowerInvariant()}/index.json",ct).ConfigureAwait(false);
            return ToolkitResult.Success(new{package=pkg,exists=resp.IsSuccessStatusCode});
        }catch{return ToolkitResult.Success(new{package=pkg,exists=false});}
    }
    private static async Task<ToolkitResult> LatestStable(string pkg,CancellationToken ct){
        var idx=await Http.Client.GetFromJsonAsync<JsonElement>($"https://api.nuget.org/v3-flatcontainer/{pkg.ToLowerInvariant()}/index.json",ct).ConfigureAwait(false);
        var versions=idx.TryGetProperty("versions",out var vs)?vs.EnumerateArray().Select(x=>x.GetString()??"").ToArray():[];
        // stable = no prerelease suffix
        var stable=versions.LastOrDefault(v=>!v.Contains('-'))??versions.LastOrDefault()??"";
        return ToolkitResult.Success(new{package=pkg,latest_stable=stable,all_versions_count=versions.Length});
    }
}

// T-53 — Docker CLI wrapper (BCL Process — docker must be in PATH)
[Toolkit("docker","Docker",ToolkitCategory.Development)]
public sealed class DockerToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"docker";public override string Name=>"Docker";
    public override ToolkitCategory Category=>ToolkitCategory.Development;
    public override bool IsConfigured=>true;
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        return a switch{
            "ps"       =>await RunDocker("ps --format json",ct).ConfigureAwait(false),
            "images"   =>await RunDocker("images --format json",ct).ConfigureAwait(false),
            "pull"     =>await RunDocker($"pull {GetRequiredParam<string>(p,"image")}",ct).ConfigureAwait(false),
            "run"      =>await RunDocker($"run {GetParam<string>(p,"flags")??"--rm"} {GetRequiredParam<string>(p,"image")} {GetParam<string>(p,"cmd")??""}", ct).ConfigureAwait(false),
            "stop"     =>await RunDocker($"stop {GetRequiredParam<string>(p,"container")}",ct).ConfigureAwait(false),
            "rm"       =>await RunDocker($"rm {GetRequiredParam<string>(p,"container")}",ct).ConfigureAwait(false),
            "logs"     =>await RunDocker($"logs {GetRequiredParam<string>(p,"container")} --tail {GetParam<int?>(p,"tail")??100}",ct).ConfigureAwait(false),
            "exec"     =>await RunDocker($"exec {GetRequiredParam<string>(p,"container")} {GetParam<string>(p,"cmd")??"sh"}",ct).ConfigureAwait(false),
            "stats"    =>await RunDocker($"stats --no-stream --format json",ct).ConfigureAwait(false),
            "inspect"  =>await RunDocker($"inspect {GetRequiredParam<string>(p,"target")}",ct).ConfigureAwait(false),
            _=>await RunDocker(GetParam<string>(p,"args")??a,ct).ConfigureAwait(false)
        };
    }
    private static async Task<ToolkitResult> RunDocker(string args,CancellationToken ct){
        var psi=new ProcessStartInfo("docker",args){RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false};
        using var proc=Process.Start(psi)??throw AgxiomeToolkitException.DependencyUnavailable("docker", "docker");
        var stdout=await proc.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
        var stderr=await proc.StandardError.ReadToEndAsync(ct).ConfigureAwait(false);
        await proc.WaitForExitAsync(ct).ConfigureAwait(false);
        object output;
        try{output=stdout.Trim().Split('\n',StringSplitOptions.RemoveEmptyEntries).Select(l=>{try{return (object)JsonDocument.Parse(l).RootElement;}catch{return l;}}).ToArray();}
        catch{output=stdout.Trim();}
        return proc.ExitCode==0?ToolkitResult.Success(new{command=$"docker {args}",output,exitCode=0}):ToolkitResult.Fail($"docker {args} failed: {stderr.Trim()}");
    }
}

// T-54 — Kubernetes kubectl wrapper (BCL Process — kubectl must be in PATH)
[Toolkit("kubernetes","Kubernetes",ToolkitCategory.Development)]
public sealed class KubernetesToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"kubernetes";public override string Name=>"Kubernetes";
    public override ToolkitCategory Category=>ToolkitCategory.Development;
    public override bool IsConfigured=>true;
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var ns=GetParam<string>(p,"namespace")??GetSetting("namespace")??"default";
        var resource=GetParam<string>(p,"resource")??"pod";
        return a switch{
            "get"       =>await RunKubectl($"get {resource} -n {ns} -o json",ct).ConfigureAwait(false),
            "describe"  =>await RunKubectl($"describe {resource} {GetParam<string>(p,"name")??""}  -n {ns}",ct).ConfigureAwait(false),
            "apply"     =>await RunKubectl($"apply -f {GetRequiredParam<string>(p,"file")} -n {ns}",ct).ConfigureAwait(false),
            "delete"    =>await RunKubectl($"delete {resource} {GetParam<string>(p,"name")??""}  -n {ns}",ct).ConfigureAwait(false),
            "logs"      =>await RunKubectl($"logs {GetRequiredParam<string>(p,"pod")} -n {ns} --tail={GetParam<int?>(p,"tail")??100}",ct).ConfigureAwait(false),
            "namespaces"=>await RunKubectl("get namespaces",ct).ConfigureAwait(false),
            "contexts"  =>await RunKubectl("config get-contexts",ct).ConfigureAwait(false),
            _=>await RunKubectl(GetParam<string>(p,"args")??a,ct).ConfigureAwait(false)
        };
    }
    private static async Task<ToolkitResult> RunKubectl(string args,CancellationToken ct){
        var psi=new ProcessStartInfo("kubectl",args){RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false};
        using var proc=Process.Start(psi)??throw AgxiomeToolkitException.DependencyUnavailable("kubernetes", "kubectl");
        var stdout=await proc.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
        var stderr=await proc.StandardError.ReadToEndAsync(ct).ConfigureAwait(false);
        await proc.WaitForExitAsync(ct).ConfigureAwait(false);
        object output;
        try{output=JsonDocument.Parse(stdout).RootElement;}catch{output=stdout.Trim();}
        return proc.ExitCode==0?ToolkitResult.Success(new{command=$"kubectl {args}",output}):ToolkitResult.Fail($"kubectl failed: {stderr.Trim()}");
    }
}

// T-55 — Terraform CLI wrapper
[Toolkit("terraform","Terraform",ToolkitCategory.Development)]
public sealed class TerraformToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"terraform";public override string Name=>"Terraform";
    public override ToolkitCategory Category=>ToolkitCategory.Development;
    public override bool IsConfigured=>true;
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var dir=GetParam<string>(p,"working_dir")??GetSetting("working_dir")??"." ;
        return a switch{
            "init"     =>await RunTf(dir,"init -no-color",ct).ConfigureAwait(false),
            "plan"     =>await RunTf(dir,"plan -no-color",ct).ConfigureAwait(false),
            "apply"    =>await RunTf(dir,"apply -auto-approve -no-color",ct).ConfigureAwait(false),
            "destroy"  =>await RunTf(dir,"destroy -auto-approve -no-color",ct).ConfigureAwait(false),
            "output"   =>await RunTf(dir,"output -json",ct).ConfigureAwait(false),
            "validate" =>await RunTf(dir,"validate -json",ct).ConfigureAwait(false),
            "show"     =>await RunTf(dir,"show -json",ct).ConfigureAwait(false),
            _=>await RunTf(dir,GetParam<string>(p,"args")??a,ct).ConfigureAwait(false)
        };
    }
    private static async Task<ToolkitResult> RunTf(string dir,string args,CancellationToken ct){
        var psi=new ProcessStartInfo("terraform",args){WorkingDirectory=dir,RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false};
        using var proc=Process.Start(psi)??throw AgxiomeToolkitException.DependencyUnavailable("terraform", "terraform");
        var stdout=await proc.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
        var stderr=await proc.StandardError.ReadToEndAsync(ct).ConfigureAwait(false);
        await proc.WaitForExitAsync(ct).ConfigureAwait(false);
        return proc.ExitCode==0?ToolkitResult.Success(new{command=$"terraform {args}",output=stdout.Trim()}):ToolkitResult.Fail($"terraform {args} failed: {stderr.Trim()}");
    }
}

// T-56 — Pulumi CLI wrapper
[Toolkit("pulumi","Pulumi",ToolkitCategory.Development)]
public sealed class PulumiToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"pulumi";public override string Name=>"Pulumi";
    public override ToolkitCategory Category=>ToolkitCategory.Development;
    public override bool IsConfigured=>true;
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var dir=GetParam<string>(p,"working_dir")??".";
        var stack=GetParam<string>(p,"stack")??GetSetting("stack")??"";
        var stackArg=string.IsNullOrWhiteSpace(stack)?"":" --stack "+stack;
        return a switch{
            "up"      =>await RunPulumi(dir,$"up --yes{stackArg}",ct).ConfigureAwait(false),
            "preview" =>await RunPulumi(dir,$"preview{stackArg}",ct).ConfigureAwait(false),
            "destroy" =>await RunPulumi(dir,$"destroy --yes{stackArg}",ct).ConfigureAwait(false),
            "outputs" =>await RunPulumi(dir,$"stack output --json{stackArg}",ct).ConfigureAwait(false),
            "status"  =>await RunPulumi(dir,$"stack{stackArg}",ct).ConfigureAwait(false),
            _=>await RunPulumi(dir,GetParam<string>(p,"args")??a,ct).ConfigureAwait(false)
        };
    }
    private static async Task<ToolkitResult> RunPulumi(string dir,string args,CancellationToken ct){
        var psi=new ProcessStartInfo("pulumi",args){WorkingDirectory=dir,RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false};
        using var proc=Process.Start(psi)??throw AgxiomeToolkitException.DependencyUnavailable("pulumi", "pulumi");
        var stdout=await proc.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
        var stderr=await proc.StandardError.ReadToEndAsync(ct).ConfigureAwait(false);
        await proc.WaitForExitAsync(ct).ConfigureAwait(false);
        return proc.ExitCode==0?ToolkitResult.Success(new{command=$"pulumi {args}",output=stdout.Trim()}):ToolkitResult.Fail($"pulumi {args} failed: {stderr.Trim()}");
    }
}

// T-57 — Helm CLI wrapper
[Toolkit("helm","Helm",ToolkitCategory.Development)]
public sealed class HelmToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"helm";public override string Name=>"Helm";
    public override ToolkitCategory Category=>ToolkitCategory.Development;
    public override bool IsConfigured=>true;
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var ns=GetParam<string>(p,"namespace")??"default";
        return a switch{
            "list"       =>await RunHelm($"list -n {ns} --output json",ct).ConfigureAwait(false),
            "install"    =>await RunHelm($"install {GetParam<string>(p,"release")??"my-release"} {GetParam<string>(p,"chart")??"."} -n {ns}",ct).ConfigureAwait(false),
            "upgrade"    =>await RunHelm($"upgrade {GetRequiredParam<string>(p,"release")} {GetParam<string>(p,"chart")??"."} -n {ns} --install",ct).ConfigureAwait(false),
            "uninstall"  =>await RunHelm($"uninstall {GetRequiredParam<string>(p,"release")} -n {ns}",ct).ConfigureAwait(false),
            "status"     =>await RunHelm($"status {GetRequiredParam<string>(p,"release")} -n {ns} --output json",ct).ConfigureAwait(false),
            "repo_add"   =>await RunHelm($"repo add {GetParam<string>(p,"name")??"repo"} {GetRequiredParam<string>(p,"url")}",ct).ConfigureAwait(false),
            _=>await RunHelm(GetParam<string>(p,"args")??a,ct).ConfigureAwait(false)
        };
    }
    private static async Task<ToolkitResult> RunHelm(string args,CancellationToken ct){
        var psi=new ProcessStartInfo("helm",args){RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false};
        using var proc=Process.Start(psi)??throw AgxiomeToolkitException.DependencyUnavailable("helm", "helm");
        var stdout=await proc.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
        var stderr=await proc.StandardError.ReadToEndAsync(ct).ConfigureAwait(false);
        await proc.WaitForExitAsync(ct).ConfigureAwait(false);
        object output;try{output=JsonDocument.Parse(stdout).RootElement;}catch{output=stdout.Trim();}
        return proc.ExitCode==0?ToolkitResult.Success(new{command=$"helm {args}",output}):ToolkitResult.Fail($"helm {args} failed: {stderr.Trim()}");
    }
}

// T-58 — AWS CLI wrapper
[Toolkit("aws_cli","AWS CLI",ToolkitCategory.Development)]
public sealed class AwsCliToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"aws_cli";public override string Name=>"AWS CLI";
    public override ToolkitCategory Category=>ToolkitCategory.Development;
    public override bool IsConfigured=>true;
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var service=GetParam<string>(p,"service")??"s3";
        var args=GetParam<string>(p,"args")??a;
        var fullArgs=$"{service} {args} --output json";
        var env=new Dictionary<string,string?>();
        if(!string.IsNullOrWhiteSpace(GetSetting("access_key_id")))env["AWS_ACCESS_KEY_ID"]=GetSetting("access_key_id");
        if(!string.IsNullOrWhiteSpace(GetSetting("secret_access_key")))env["AWS_SECRET_ACCESS_KEY"]=GetSetting("secret_access_key");
        if(!string.IsNullOrWhiteSpace(GetSetting("region")))env["AWS_DEFAULT_REGION"]=GetSetting("region");
        return await RunCli("aws",fullArgs,env,ct).ConfigureAwait(false);
    }
    private static async Task<ToolkitResult> RunCli(string exe,string args,Dictionary<string,string?> env,CancellationToken ct){
        var psi=new ProcessStartInfo(exe,args){RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false};
        foreach(var kv in env)psi.Environment[kv.Key]=kv.Value;
        using var proc=Process.Start(psi)??throw AgxiomeToolkitException.DependencyUnavailable("aws_cli", exe);
        var stdout=await proc.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
        var stderr=await proc.StandardError.ReadToEndAsync(ct).ConfigureAwait(false);
        await proc.WaitForExitAsync(ct).ConfigureAwait(false);
        object output;try{output=JsonDocument.Parse(stdout).RootElement;}catch{output=stdout.Trim();}
        return proc.ExitCode==0?ToolkitResult.Success(new{command=$"{exe} {args}",output}):ToolkitResult.Fail($"{exe} failed: {stderr.Trim()}");
    }
}

// T-59 — Azure CLI wrapper
[Toolkit("azure_cli","Azure CLI",ToolkitCategory.Development)]
public sealed class AzureCliToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"azure_cli";public override string Name=>"Azure CLI";
    public override ToolkitCategory Category=>ToolkitCategory.Development;
    public override bool IsConfigured=>true;
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var args=(GetParam<string>(p,"args")??a)+" --output json";
        var psi=new ProcessStartInfo("az",args){RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false};
        using var proc=Process.Start(psi)??throw AgxiomeToolkitException.DependencyUnavailable("azure_cli", "az");
        var stdout=await proc.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
        var stderr=await proc.StandardError.ReadToEndAsync(ct).ConfigureAwait(false);
        await proc.WaitForExitAsync(ct).ConfigureAwait(false);
        object output;try{output=JsonDocument.Parse(stdout).RootElement;}catch{output=stdout.Trim();}
        return proc.ExitCode==0?ToolkitResult.Success(new{command=$"az {args}",output}):ToolkitResult.Fail($"az failed: {stderr.Trim()}");
    }
}

// T-60 — GCP gcloud CLI wrapper
[Toolkit("gcp_cli","GCP CLI",ToolkitCategory.Development)]
public sealed class GcpCliToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"gcp_cli";public override string Name=>"GCP CLI";
    public override ToolkitCategory Category=>ToolkitCategory.Development;
    public override bool IsConfigured=>true;
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var args=(GetParam<string>(p,"args")??a)+" --format=json";
        var psi=new ProcessStartInfo("gcloud",args){RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false};
        using var proc=Process.Start(psi)??throw AgxiomeToolkitException.DependencyUnavailable("gcp_cli", "gcloud");
        var stdout=await proc.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
        var stderr=await proc.StandardError.ReadToEndAsync(ct).ConfigureAwait(false);
        await proc.WaitForExitAsync(ct).ConfigureAwait(false);
        object output;try{output=JsonDocument.Parse(stdout).RootElement;}catch{output=stdout.Trim();}
        return proc.ExitCode==0?ToolkitResult.Success(new{command=$"gcloud {args}",output}):ToolkitResult.Fail($"gcloud failed: {stderr.Trim()}");
    }
}

// T-61 — System Info (pure BCL, no credentials needed)
[Toolkit("system_info","System Info",ToolkitCategory.Development)]
public sealed class SystemInfoToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"system_info";public override string Name=>"System Info";
    public override ToolkitCategory Category=>ToolkitCategory.Development;
    public override bool IsConfigured=>true;
    protected override Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var mem=GC.GetGCMemoryInfo();
        return Task.FromResult(ToolkitResult.Success(new{
            os=Environment.OSVersion.ToString(),
            platform=System.Runtime.InteropServices.RuntimeInformation.OSDescription,
            architecture=System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString(),
            processorCount=Environment.ProcessorCount,
            totalMemoryBytes=mem.TotalAvailableMemoryBytes,
            heapSizeBytes=GC.GetTotalMemory(false),
            dotnetVersion=Environment.Version.ToString(),
            machineName=Environment.MachineName,
            userName=Environment.UserName,
            currentDirectory=Environment.CurrentDirectory,
            tempPath=Path.GetTempPath(),
            drives=DriveInfo.GetDrives().Select(d=>new{d.Name,d.DriveType,totalGB=d.IsReady?Math.Round(d.TotalSize/1e9,1):0.0,freeGB=d.IsReady?Math.Round(d.AvailableFreeSpace/1e9,1):0.0}).ToArray()
        }));
    }
}

// T-62 — File System (BCL — wraps LocalFileToolkit for development context)
[Toolkit("filesystem","File System",ToolkitCategory.Development)]
public sealed class FileSystemToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"filesystem";public override string Name=>"File System";
    public override ToolkitCategory Category=>ToolkitCategory.Development;
    public override bool IsConfigured=>true;
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var path=GetParam<string>(p,"path")??".";
        return a switch{
            "list"       =>Task.FromResult(ToolkitResult.Success(new{path,entries=Directory.Exists(path)?Directory.GetFileSystemEntries(path,GetParam<string>(p,"pattern")??"*",GetParam<bool?>(p,"recursive")??false?SearchOption.AllDirectories:SearchOption.TopDirectoryOnly).Select(e=>new{name=Path.GetFileName(e),isDir=Directory.Exists(e),sizeBytes=File.Exists(e)?new FileInfo(e).Length:0L}).ToArray():[]})).Result,
            "read"       =>File.Exists(path)?ToolkitResult.Success(File.ReadAllText(path)):ToolkitResult.Fail($"File not found: {path}"),
            "write"      =>await WriteFile(path,GetParam<string>(p,"content")??"",ct).ConfigureAwait(false),
            "delete"     =>DeletePath(path),
            "exists"     =>ToolkitResult.Success(new{path,exists=File.Exists(path)||Directory.Exists(path),isFile=File.Exists(path),isDir=Directory.Exists(path)}),
            "tree"       =>ToolkitResult.Success(new{path,tree=BuildTree(path,GetParam<int?>(p,"depth")??3)}),
            _            =>throw AgxiomeToolkitException.UnknownAction(Id, a)
        };
    }
    private static async Task<ToolkitResult> WriteFile(string path,string content,CancellationToken ct){
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path,content,ct).ConfigureAwait(false);
        return ToolkitResult.Success(new{path,bytesWritten=content.Length});
    }
    private static ToolkitResult DeletePath(string path){
        if(File.Exists(path)){File.Delete(path);return ToolkitResult.Success(new{path,deleted=true,type="file"});}
        if(Directory.Exists(path)){Directory.Delete(path,true);return ToolkitResult.Success(new{path,deleted=true,type="directory"});}
        return ToolkitResult.Fail($"Path not found: {path}");
    }
    private static object BuildTree(string dir,int depth){
        if(depth<=0||!Directory.Exists(dir))return new{};
        return new{name=Path.GetFileName(dir),type="dir",
            children=Directory.GetFileSystemEntries(dir).Take(50).Select(e=>Directory.Exists(e)?(object)BuildTree(e,depth-1):new{name=Path.GetFileName(e),type="file",sizeBytes=new FileInfo(e).Length}).ToArray()};
    }
}

// T-63 — Shell execution (opt-in via enabled=true in config — SECURITY: disabled by default)
[Toolkit("shell","Shell",ToolkitCategory.Development)]
public sealed class ShellToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"shell";public override string Name=>"Shell";
    public override ToolkitCategory Category=>ToolkitCategory.Development;
    public override bool IsConfigured=>GetSetting("enabled")=="true";
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        if(!IsConfigured)return ToolkitResult.Fail("Shell execution requires explicit enabled=true in ToolkitConfig.Settings for security.");
        var cmd=GetParam<string>(p,"command")??GetParam<string>(p,"cmd")??a;
        if(string.IsNullOrWhiteSpace(cmd))return ToolkitResult.Fail("command parameter required");
        var workDir=GetParam<string>(p,"working_dir")??Directory.GetCurrentDirectory();
        var timeoutSec=GetParam<int?>(p,"timeout_seconds")??30;
        bool isWindows=System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows);
        var psi=isWindows
            ?new ProcessStartInfo("cmd","/c "+cmd){WorkingDirectory=workDir,RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false}
            :new ProcessStartInfo("/bin/sh","-c "+cmd){WorkingDirectory=workDir,RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false};
        using var cts=CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(timeoutSec));
        using var proc=Process.Start(psi)??throw AgxiomeToolkitException.DependencyUnavailable("shell", isWindows ? "cmd" : "/bin/sh");
        var stdout=await proc.StandardOutput.ReadToEndAsync(cts.Token).ConfigureAwait(false);
        var stderr=await proc.StandardError.ReadToEndAsync(cts.Token).ConfigureAwait(false);
        await proc.WaitForExitAsync(cts.Token).ConfigureAwait(false);
        return ToolkitResult.Success(new{command=cmd,stdout=stdout.Trim(),stderr=stderr.Trim(),exitCode=proc.ExitCode,success=proc.ExitCode==0});
    }
}

// T-64 — Python Runner (BCL Process — python/python3 must be in PATH)
[Toolkit("python_runner","Python Runner",ToolkitCategory.Development)]
public sealed class PythonRunnerToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"python_runner";public override string Name=>"Python Runner";
    public override ToolkitCategory Category=>ToolkitCategory.Development;
    public override bool IsConfigured=>GetSetting("enabled")=="true";
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        if(!IsConfigured)return ToolkitResult.Fail("Python runner requires enabled=true in config.");
        var pyExe=GetSetting("python_executable")??"python3";
        string? scriptFile=null;
        try{
            var script=GetParam<string>(p,"script")??GetParam<string>(p,"code")??"";
            if(string.IsNullOrWhiteSpace(script)&&a!="run_file")return ToolkitResult.Fail("script or code parameter required");
            if(a=="run_file"){
                scriptFile=GetRequiredParam<string>(p,"file");
            }else{
                scriptFile=Path.Combine(Path.GetTempPath(),$"agx_{Guid.NewGuid():N}.py");
                await File.WriteAllTextAsync(scriptFile,script,ct).ConfigureAwait(false);
            }
            var psi=new ProcessStartInfo(pyExe,scriptFile){RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false};
            var envVars=GetParam<Dictionary<string,string>?>(p,"env");
            if(envVars!=null)foreach(var kv in envVars)psi.Environment[kv.Key]=kv.Value;
            using var cts=CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(GetParam<int?>(p,"timeout_seconds")??60));
            using var proc=Process.Start(psi)??throw AgxiomeToolkitException.DependencyUnavailable("python_runner", pyExe);
            var stdout=await proc.StandardOutput.ReadToEndAsync(cts.Token).ConfigureAwait(false);
            var stderr=await proc.StandardError.ReadToEndAsync(cts.Token).ConfigureAwait(false);
            await proc.WaitForExitAsync(cts.Token).ConfigureAwait(false);
            return ToolkitResult.Success(new{stdout=stdout.Trim(),stderr=stderr.Trim(),returnCode=proc.ExitCode,success=proc.ExitCode==0});
        }finally{
            if(a!="run_file"&&scriptFile!=null&&File.Exists(scriptFile))File.Delete(scriptFile);
        }
    }
}

// T-65 — Jupyter (requires running Jupyter server — server_url + token/password)
[Toolkit("jupyter","Jupyter",ToolkitCategory.Development)]
public sealed class JupyterToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"jupyter";public override string Name=>"Jupyter";
    public override ToolkitCategory Category=>ToolkitCategory.Development;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("server_url"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var baseUrl=(GetSetting("server_url")??"").TrimEnd('/');
        var token=GetSetting("token")??"";
        var tokenParam=string.IsNullOrWhiteSpace(token)?"":$"?token={token}";
        return a switch{
            "list_kernels"   =>await JupyterGet($"{baseUrl}/api/kernels{tokenParam}",token,ct).ConfigureAwait(false),
            "list_notebooks" =>await JupyterGet($"{baseUrl}/api/contents{tokenParam}",token,ct).ConfigureAwait(false),
            "get_notebook"   =>await JupyterGet($"{baseUrl}/api/contents/{GetRequiredParam<string>(p,"path")}{tokenParam}",token,ct).ConfigureAwait(false),
            _=>throw AgxiomeToolkitException.UnknownAction(Id, a)
        };
    }
    private static async Task<ToolkitResult> JupyterGet(string url,string token,CancellationToken ct){
        var req=new HttpRequestMessage(HttpMethod.Get,url);
        if(!string.IsNullOrWhiteSpace(token))req.Headers.Add("Authorization",$"token {token}");
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
}

// T-66 — Build Tools (dotnet/msbuild/make via BCL Process)
[Toolkit("build_tools","Build Tools",ToolkitCategory.Development)]
public sealed class BuildToolsToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"build_tools";public override string Name=>"Build Tools";
    public override ToolkitCategory Category=>ToolkitCategory.Development;
    public override bool IsConfigured=>true;
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var workDir=GetParam<string>(p,"working_dir")??".";
        var tool=GetParam<string>(p,"tool")??"dotnet";
        var args=a switch{
            "build"   =>tool=="dotnet"?$"build {GetParam<string>(p,"project")??"."} -c {GetParam<string>(p,"configuration")??"Release"}":$"build {GetParam<string>(p,"target")??""}",
            "test"    =>tool=="dotnet"?$"test {GetParam<string>(p,"project")??"."} --no-build":GetParam<string>(p,"args")??"test",
            "publish" =>tool=="dotnet"?$"publish {GetParam<string>(p,"project")??"."} -c Release -o {GetParam<string>(p,"output")??"./publish"}":"publish",
            "restore" =>tool=="dotnet"?"restore":"install",
            "clean"   =>tool=="dotnet"?$"clean {GetParam<string>(p,"project")??"."} ":"clean",
            _         =>GetParam<string>(p,"args")??a
        };
        var psi=new ProcessStartInfo(tool,args){WorkingDirectory=workDir,RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false};
        using var proc=Process.Start(psi)??throw AgxiomeToolkitException.DependencyUnavailable("build_tools", tool);
        var stdout=await proc.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
        var stderr=await proc.StandardError.ReadToEndAsync(ct).ConfigureAwait(false);
        await proc.WaitForExitAsync(ct).ConfigureAwait(false);
        return ToolkitResult.Success(new{command=$"{tool} {args}",stdout=stdout.Trim(),stderr=stderr.Trim(),exitCode=proc.ExitCode,success=proc.ExitCode==0});
    }
}

// T-67 — Code Quality (runs dotnet format, eslint, pylint, etc.)
[Toolkit("code_quality","Code Quality",ToolkitCategory.Development)]
public sealed class CodeQualityToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"code_quality";public override string Name=>"Code Quality";
    public override ToolkitCategory Category=>ToolkitCategory.Development;
    public override bool IsConfigured=>true;
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var tool=GetParam<string>(p,"tool")??"dotnet-format";
        var target=GetParam<string>(p,"target")??"." ;
        var (exe,args)=tool switch{
            "dotnet-format" =>("dotnet",$"format {target} --verify-no-changes --report json"),
            "eslint"        =>("npx",$"eslint {target} --format json"),
            "pylint"        =>("pylint",$"{target} --output-format json"),
            "sonar"         =>("sonar-scanner",GetParam<string>(p,"args")??""),
            _               =>(tool,GetParam<string>(p,"args")??target)
        };
        var psi=new ProcessStartInfo(exe,args){WorkingDirectory=GetParam<string>(p,"working_dir")??".",RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false};
        using var proc=Process.Start(psi)??throw AgxiomeToolkitException.DependencyUnavailable("code_quality", exe);
        var stdout=await proc.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
        var stderr=await proc.StandardError.ReadToEndAsync(ct).ConfigureAwait(false);
        await proc.WaitForExitAsync(ct).ConfigureAwait(false);
        object output;try{output=JsonDocument.Parse(stdout).RootElement;}catch{output=stdout.Trim();}
        return ToolkitResult.Success(new{tool,target,output,issues=proc.ExitCode==0?0:-1,exitCode=proc.ExitCode});
    }
}
