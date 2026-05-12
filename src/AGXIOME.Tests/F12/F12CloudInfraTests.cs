using AGXIOME.Toolkits;
using AGXIOME.Toolkits.CloudInfra;

namespace AGXIOME.Tests.F12;

public sealed class AwsS3ToolkitTests
{
    [Fact] public void Id_IsCorrect() => Assert.Equal("aws_s3", new AwsS3Toolkit().Id);
    [Fact] public void Name_IsCorrect() => Assert.Equal("AWS S3", new AwsS3Toolkit().Name);
    [Fact] public void Category_IsCorrect() => Assert.Equal(ToolkitCategory.CloudInfra, new AwsS3Toolkit().Category);
    [Fact] public void NotConfigured_ReturnsSkipped()
    {
        var r = new AwsS3Toolkit().ExecuteAsync("list_buckets", [], default).GetAwaiter().GetResult();
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("aws_s3", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new AwsS3Toolkit(cfg).ExecuteAsync("list_buckets", new Dictionary<string, object?> { ["region"] = "us-east-1" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class AwsLambdaToolkitTests
{
    [Fact] public void Id_IsCorrect() => Assert.Equal("aws_lambda", new AwsLambdaToolkit().Id);
    [Fact] public void Name_IsCorrect() => Assert.Equal("AWS Lambda", new AwsLambdaToolkit().Name);
    [Fact] public void Category_IsCorrect() => Assert.Equal(ToolkitCategory.CloudInfra, new AwsLambdaToolkit().Category);
    [Fact] public void NotConfigured_ReturnsSkipped()
    {
        var r = new AwsLambdaToolkit().ExecuteAsync("invoke", [], default).GetAwaiter().GetResult();
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("aws_lambda", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new AwsLambdaToolkit(cfg).ExecuteAsync("invoke", new Dictionary<string, object?> { ["function_name"] = "fn", ["payload"] = "{}" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class AwsDynamoDbToolkitTests
{
    [Fact] public void Id_IsCorrect() => Assert.Equal("aws_dynamodb", new AwsDynamoDbToolkit().Id);
    [Fact] public void Name_IsCorrect() => Assert.Equal("AWS DynamoDB", new AwsDynamoDbToolkit().Name);
    [Fact] public void Category_IsCorrect() => Assert.Equal(ToolkitCategory.CloudInfra, new AwsDynamoDbToolkit().Category);
    [Fact] public void NotConfigured_ReturnsSkipped()
    {
        var r = new AwsDynamoDbToolkit().ExecuteAsync("get_item", [], default).GetAwaiter().GetResult();
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("aws_dynamodb", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new AwsDynamoDbToolkit(cfg).ExecuteAsync("get_item", new Dictionary<string, object?> { ["table"] = "t", ["key"] = "k" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class AwsEc2ToolkitTests
{
    [Fact] public void Id_IsCorrect() => Assert.Equal("aws_ec2", new AwsEc2Toolkit().Id);
    [Fact] public void Name_IsCorrect() => Assert.Equal("AWS EC2", new AwsEc2Toolkit().Name);
    [Fact] public void Category_IsCorrect() => Assert.Equal(ToolkitCategory.CloudInfra, new AwsEc2Toolkit().Category);
    [Fact] public void NotConfigured_ReturnsSkipped()
    {
        var r = new AwsEc2Toolkit().ExecuteAsync("list_instances", [], default).GetAwaiter().GetResult();
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("aws_ec2", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new AwsEc2Toolkit(cfg).ExecuteAsync("list_instances", new Dictionary<string, object?> { ["region"] = "us-east-1" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class AzureBlobToolkitTests
{
    [Fact] public void Id_IsCorrect() => Assert.Equal("azure_blob", new AzureBlobToolkit().Id);
    [Fact] public void Name_IsCorrect() => Assert.Equal("Azure Blob", new AzureBlobToolkit().Name);
    [Fact] public void Category_IsCorrect() => Assert.Equal(ToolkitCategory.CloudInfra, new AzureBlobToolkit().Category);
    [Fact] public void NotConfigured_ReturnsSkipped()
    {
        var r = new AzureBlobToolkit().ExecuteAsync("list_blobs", [], default).GetAwaiter().GetResult();
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("azure_blob", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new AzureBlobToolkit(cfg).ExecuteAsync("list_blobs", new Dictionary<string, object?> { ["container"] = "c" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class AzureFunctionsToolkitTests
{
    [Fact] public void Id_IsCorrect() => Assert.Equal("azure_functions", new AzureFunctionsToolkit().Id);
    [Fact] public void Name_IsCorrect() => Assert.Equal("Azure Functions", new AzureFunctionsToolkit().Name);
    [Fact] public void Category_IsCorrect() => Assert.Equal(ToolkitCategory.CloudInfra, new AzureFunctionsToolkit().Category);
    [Fact] public void NotConfigured_ReturnsSkipped()
    {
        var r = new AzureFunctionsToolkit().ExecuteAsync("invoke", [], default).GetAwaiter().GetResult();
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("azure_functions", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new AzureFunctionsToolkit(cfg).ExecuteAsync("invoke", new Dictionary<string, object?> { ["function_name"] = "fn", ["payload"] = "{}" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class GcpStorageToolkitTests
{
    [Fact] public void Id_IsCorrect() => Assert.Equal("gcp_storage", new GcpStorageToolkit().Id);
    [Fact] public void Name_IsCorrect() => Assert.Equal("GCP Storage", new GcpStorageToolkit().Name);
    [Fact] public void Category_IsCorrect() => Assert.Equal(ToolkitCategory.CloudInfra, new GcpStorageToolkit().Category);
    [Fact] public void NotConfigured_ReturnsSkipped()
    {
        var r = new GcpStorageToolkit().ExecuteAsync("list_objects", [], default).GetAwaiter().GetResult();
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("gcp_storage", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new GcpStorageToolkit(cfg).ExecuteAsync("list_objects", new Dictionary<string, object?> { ["bucket"] = "b" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class GcpCloudRunToolkitTests
{
    [Fact] public void Id_IsCorrect() => Assert.Equal("gcp_cloudrun", new GcpCloudRunToolkit().Id);
    [Fact] public void Name_IsCorrect() => Assert.Equal("GCP Cloud Run", new GcpCloudRunToolkit().Name);
    [Fact] public void Category_IsCorrect() => Assert.Equal(ToolkitCategory.CloudInfra, new GcpCloudRunToolkit().Category);
    [Fact] public void NotConfigured_ReturnsSkipped()
    {
        var r = new GcpCloudRunToolkit().ExecuteAsync("list_services", [], default).GetAwaiter().GetResult();
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("gcp_cloud_run", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new GcpCloudRunToolkit(cfg).ExecuteAsync("list_services", new Dictionary<string, object?> { ["region"] = "us-central1" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class CloudflareToolkitTests
{
    [Fact] public void Id_IsCorrect() => Assert.Equal("cloudflare", new CloudflareToolkit().Id);
    [Fact] public void Name_IsCorrect() => Assert.Equal("Cloudflare", new CloudflareToolkit().Name);
    [Fact] public void Category_IsCorrect() => Assert.Equal(ToolkitCategory.CloudInfra, new CloudflareToolkit().Category);
    [Fact] public void NotConfigured_ReturnsSkipped()
    {
        var r = new CloudflareToolkit().ExecuteAsync("list_zones", [], default).GetAwaiter().GetResult();
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("cloudflare", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new CloudflareToolkit(cfg).ExecuteAsync("list_zones", new Dictionary<string, object?> { ["account_id"] = "1" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class VercelToolkitTests
{
    [Fact] public void Id_IsCorrect() => Assert.Equal("vercel", new VercelToolkit().Id);
    [Fact] public void Name_IsCorrect() => Assert.Equal("Vercel", new VercelToolkit().Name);
    [Fact] public void Category_IsCorrect() => Assert.Equal(ToolkitCategory.CloudInfra, new VercelToolkit().Category);
    [Fact] public void NotConfigured_ReturnsSkipped()
    {
        var r = new VercelToolkit().ExecuteAsync("list_deployments", [], default).GetAwaiter().GetResult();
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("vercel", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new VercelToolkit(cfg).ExecuteAsync("list_deployments", new Dictionary<string, object?> { ["project_id"] = "p" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class NetlifyToolkitTests
{
    [Fact] public void Id_IsCorrect() => Assert.Equal("netlify", new NetlifyToolkit().Id);
    [Fact] public void Name_IsCorrect() => Assert.Equal("Netlify", new NetlifyToolkit().Name);
    [Fact] public void Category_IsCorrect() => Assert.Equal(ToolkitCategory.CloudInfra, new NetlifyToolkit().Category);
    [Fact] public void NotConfigured_ReturnsSkipped()
    {
        var r = new NetlifyToolkit().ExecuteAsync("list_sites", [], default).GetAwaiter().GetResult();
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("netlify", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new NetlifyToolkit(cfg).ExecuteAsync("list_sites", new Dictionary<string, object?> { ["filter"] = "all" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class DigitalOceanToolkitTests
{
    [Fact] public void Id_IsCorrect() => Assert.Equal("digitalocean", new DigitalOceanToolkit().Id);
    [Fact] public void Name_IsCorrect() => Assert.Equal("DigitalOcean", new DigitalOceanToolkit().Name);
    [Fact] public void Category_IsCorrect() => Assert.Equal(ToolkitCategory.CloudInfra, new DigitalOceanToolkit().Category);
    [Fact] public void NotConfigured_ReturnsSkipped()
    {
        var r = new DigitalOceanToolkit().ExecuteAsync("list_droplets", [], default).GetAwaiter().GetResult();
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("digitalocean", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new DigitalOceanToolkit(cfg).ExecuteAsync("list_droplets", new Dictionary<string, object?> { ["tag"] = "web" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}
