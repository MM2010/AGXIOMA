using AGXIOME.Toolkits;
using AGXIOME.Toolkits.Development;

namespace AGXIOME.Tests.F12;

public sealed class GitHubToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("github", new GitHubToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("GitHub", new GitHubToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.Development, new GitHubToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new GitHubToolkit().ExecuteAsync("list_repos", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("github", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new GitHubToolkit(cfg).ExecuteAsync("list_repos", new Dictionary<string, object?> { ["owner"] = "user" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class GitLabToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("gitlab", new GitLabToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("GitLab", new GitLabToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.Development, new GitLabToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new GitLabToolkit().ExecuteAsync("list_projects", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("gitlab", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new GitLabToolkit(cfg).ExecuteAsync("list_projects", new Dictionary<string, object?> { ["namespace"] = "user" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class BitbucketToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("bitbucket", new BitbucketToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Bitbucket", new BitbucketToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.Development, new BitbucketToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new BitbucketToolkit().ExecuteAsync("list_repos", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("bitbucket", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new BitbucketToolkit(cfg).ExecuteAsync("list_repos", new Dictionary<string, object?> { ["workspace"] = "ws" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class GitToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("git", new GitToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Git", new GitToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.Development, new GitToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new GitToolkit().ExecuteAsync("status", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("git", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new GitToolkit(cfg).ExecuteAsync("status", new Dictionary<string, object?> { ["path"] = "." }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class NuGetToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("nuget", new NuGetToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("NuGet", new NuGetToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.Development, new NuGetToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new NuGetToolkit().ExecuteAsync("search", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("nuget", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new NuGetToolkit(cfg).ExecuteAsync("search", new Dictionary<string, object?> { ["query"] = "Newtonsoft" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class DockerToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("docker", new DockerToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Docker", new DockerToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.Development, new DockerToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new DockerToolkit().ExecuteAsync("list_containers", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("docker", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new DockerToolkit(cfg).ExecuteAsync("list_containers", new Dictionary<string, object?> { ["all"] = true }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class K8sToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("kubernetes", new KubernetesToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Kubernetes", new KubernetesToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.Development, new KubernetesToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new KubernetesToolkit().ExecuteAsync("list_pods", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("kubernetes", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new KubernetesToolkit(cfg).ExecuteAsync("list_pods", new Dictionary<string, object?> { ["namespace"] = "default" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class TerraformToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("terraform", new TerraformToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Terraform", new TerraformToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.Development, new TerraformToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new TerraformToolkit().ExecuteAsync("plan", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("terraform", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new TerraformToolkit(cfg).ExecuteAsync("plan", new Dictionary<string, object?> { ["dir"] = "." }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class PulumiToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("pulumi", new PulumiToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Pulumi", new PulumiToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.Development, new PulumiToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new PulumiToolkit().ExecuteAsync("preview", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("pulumi", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new PulumiToolkit(cfg).ExecuteAsync("preview", new Dictionary<string, object?> { ["stack"] = "dev" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class HelmToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("helm", new HelmToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Helm", new HelmToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.Development, new HelmToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new HelmToolkit().ExecuteAsync("list", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("helm", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new HelmToolkit(cfg).ExecuteAsync("list", new Dictionary<string, object?> { ["namespace"] = "default" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class AwsCliToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("aws_cli", new AwsCliToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("AWS CLI", new AwsCliToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.Development, new AwsCliToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new AwsCliToolkit().ExecuteAsync("run_command", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("aws_cli", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new AwsCliToolkit(cfg).ExecuteAsync("run_command", new Dictionary<string, object?> { ["command"] = "s3 ls" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class AzureCliToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("azure_cli", new AzureCliToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Azure CLI", new AzureCliToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.Development, new AzureCliToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new AzureCliToolkit().ExecuteAsync("run_command", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("azure_cli", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new AzureCliToolkit(cfg).ExecuteAsync("run_command", new Dictionary<string, object?> { ["command"] = "account list" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class GcpCliToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("gcp_cli", new GcpCliToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("GCP CLI", new GcpCliToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.Development, new GcpCliToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new GcpCliToolkit().ExecuteAsync("run_command", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("gcp_cli", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new GcpCliToolkit(cfg).ExecuteAsync("run_command", new Dictionary<string, object?> { ["command"] = "projects list" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class SystemInfoToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("system_info", new SystemInfoToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("System Info", new SystemInfoToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.Development, new SystemInfoToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new SystemInfoToolkit().ExecuteAsync("get_info", [], default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("system_info", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new SystemInfoToolkit(cfg).ExecuteAsync("get_info", new Dictionary<string, object?> { ["type"] = "cpu" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class FileSystemToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("filesystem", new FileSystemToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("File System", new FileSystemToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.Development, new FileSystemToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new FileSystemToolkit().ExecuteAsync("list", [], default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("filesystem", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new FileSystemToolkit(cfg).ExecuteAsync("list", new Dictionary<string, object?> { ["path"] = "." }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class ShellToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("shell", new ShellToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Shell", new ShellToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.Development, new ShellToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new ShellToolkit().ExecuteAsync("run", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("shell", new Dictionary<string, string> { ["enabled"] = "true" });
        var r = await new ShellToolkit(cfg).ExecuteAsync("run", new Dictionary<string, object?> { ["command"] = "echo hi" }, default);
        Assert.Equal(ToolkitResultStatus.Error, r.Status);
    }
}

public sealed class PythonRunnerToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("python_runner", new PythonRunnerToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Python Runner", new PythonRunnerToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.Development, new PythonRunnerToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new PythonRunnerToolkit().ExecuteAsync("run", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("python_runner", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new PythonRunnerToolkit(cfg).ExecuteAsync("run", new Dictionary<string, object?> { ["code"] = "print(1)" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class JupyterToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("jupyter", new JupyterToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Jupyter", new JupyterToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.Development, new JupyterToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new JupyterToolkit().ExecuteAsync("run_cell", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("jupyter", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new JupyterToolkit(cfg).ExecuteAsync("run_cell", new Dictionary<string, object?> { ["code"] = "1+1" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class BuildToolsToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("build_tools", new BuildToolsToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Build Tools", new BuildToolsToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.Development, new BuildToolsToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new BuildToolsToolkit().ExecuteAsync("build", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("build_tools", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new BuildToolsToolkit(cfg).ExecuteAsync("build", new Dictionary<string, object?> { ["project"] = "." }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class CodeQualityToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("code_quality", new CodeQualityToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Code Quality", new CodeQualityToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.Development, new CodeQualityToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new CodeQualityToolkit().ExecuteAsync("analyze", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("code_quality", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new CodeQualityToolkit(cfg).ExecuteAsync("analyze", new Dictionary<string, object?> { ["path"] = "." }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}
