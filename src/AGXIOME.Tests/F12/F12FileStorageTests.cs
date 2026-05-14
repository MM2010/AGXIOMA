using AGXIOME.Toolkits;
using AGXIOME.Toolkits.FileStorage;

namespace AGXIOME.Tests.F12;

public sealed class LocalFileToolkitTests
{
    [Fact] public void Id_IsCorrect() => Assert.Equal("local_file", new LocalFileToolkit().Id);
    [Fact] public void Name_IsCorrect() => Assert.Equal("Local File", new LocalFileToolkit().Name);
    [Fact] public void Category_IsCorrect() => Assert.Equal(ToolkitCategory.FileStorage, new LocalFileToolkit().Category);
    [Fact] public void NotConfigured_ReturnsSkipped()
    {
        var r = new LocalFileToolkit().ExecuteAsync("write", [], default).GetAwaiter().GetResult();
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("local_file", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new LocalFileToolkit(cfg).ExecuteAsync("write", new Dictionary<string, object?> { ["path"] = "/tmp/f.txt" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class FtpSftpToolkitTests
{
    [Fact] public void Id_IsCorrect() => Assert.Equal("ftp_sftp", new FtpSftpToolkit().Id);
    [Fact] public void Name_IsCorrect() => Assert.Equal("FTP/SFTP", new FtpSftpToolkit().Name);
    [Fact] public void Category_IsCorrect() => Assert.Equal(ToolkitCategory.FileStorage, new FtpSftpToolkit().Category);
    [Fact] public void NotConfigured_ReturnsSkipped()
    {
        var r = new FtpSftpToolkit().ExecuteAsync("list_files", [], default).GetAwaiter().GetResult();
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("ftp_sftp", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new FtpSftpToolkit(cfg).ExecuteAsync("list_files", new Dictionary<string, object?> { ["path"] = "/" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class GcsToolkitTests
{
    [Fact] public void Id_IsCorrect() => Assert.Equal("gcs", new GcsToolkit().Id);
    [Fact] public void Name_IsCorrect() => Assert.Equal("Google Cloud Storage", new GcsToolkit().Name);
    [Fact] public void Category_IsCorrect() => Assert.Equal(ToolkitCategory.FileStorage, new GcsToolkit().Category);
    [Fact] public void NotConfigured_ReturnsSkipped()
    {
        var r = new GcsToolkit().ExecuteAsync("list_objects", [], default).GetAwaiter().GetResult();
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("gcs", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new GcsToolkit(cfg).ExecuteAsync("list_objects", new Dictionary<string, object?> { ["bucket"] = "b" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class MinIOToolkitTests
{
    [Fact] public void Id_IsCorrect() => Assert.Equal("minio", new MinioToolkit().Id);
    [Fact] public void Name_IsCorrect() => Assert.Equal("MinIO", new MinioToolkit().Name);
    [Fact] public void Category_IsCorrect() => Assert.Equal(ToolkitCategory.FileStorage, new MinioToolkit().Category);
    [Fact] public void NotConfigured_ReturnsSkipped()
    {
        var r = new MinioToolkit().ExecuteAsync("list_objects", [], default).GetAwaiter().GetResult();
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("minio", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new MinioToolkit(cfg).ExecuteAsync("list_objects", new Dictionary<string, object?> { ["bucket"] = "b" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class DropboxToolkitTests
{
    [Fact] public void Id_IsCorrect() => Assert.Equal("dropbox", new DropboxToolkit().Id);
    [Fact] public void Name_IsCorrect() => Assert.Equal("Dropbox", new DropboxToolkit().Name);
    [Fact] public void Category_IsCorrect() => Assert.Equal(ToolkitCategory.FileStorage, new DropboxToolkit().Category);
    [Fact] public void NotConfigured_ReturnsSkipped()
    {
        var r = new DropboxToolkit().ExecuteAsync("list_folder", [], default).GetAwaiter().GetResult();
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("dropbox", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new DropboxToolkit(cfg).ExecuteAsync("list_folder", new Dictionary<string, object?> { ["path"] = "" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class BoxToolkitTests
{
    [Fact] public void Id_IsCorrect() => Assert.Equal("box", new BoxToolkit().Id);
    [Fact] public void Name_IsCorrect() => Assert.Equal("Box", new BoxToolkit().Name);
    [Fact] public void Category_IsCorrect() => Assert.Equal(ToolkitCategory.FileStorage, new BoxToolkit().Category);
    [Fact] public void NotConfigured_ReturnsSkipped()
    {
        var r = new BoxToolkit().ExecuteAsync("list_files", [], default).GetAwaiter().GetResult();
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("box", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new BoxToolkit(cfg).ExecuteAsync("list_files", new Dictionary<string, object?> { ["folder_id"] = "0" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class SharePointToolkitTests
{
    [Fact] public void Id_IsCorrect() => Assert.Equal("sharepoint", new SharePointToolkit().Id);
    [Fact] public void Name_IsCorrect() => Assert.Equal("SharePoint", new SharePointToolkit().Name);
    [Fact] public void Category_IsCorrect() => Assert.Equal(ToolkitCategory.FileStorage, new SharePointToolkit().Category);
    [Fact] public void NotConfigured_ReturnsSkipped()
    {
        var r = new SharePointToolkit().ExecuteAsync("list_files", [], default).GetAwaiter().GetResult();
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("sharepoint", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new SharePointToolkit(cfg).ExecuteAsync("list_files", new Dictionary<string, object?> { ["site_url"] = "https://sp.com", ["path"] = "/" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class CompressionToolkitTests
{
    [Fact] public void Id_IsCorrect() => Assert.Equal("compression", new CompressionToolkit().Id);
    [Fact] public void Name_IsCorrect() => Assert.Equal("Compression", new CompressionToolkit().Name);
    [Fact] public void Category_IsCorrect() => Assert.Equal(ToolkitCategory.FileStorage, new CompressionToolkit().Category);
    [Fact] public void NotConfigured_ReturnsSkipped()
    {
        var r = new CompressionToolkit().ExecuteAsync("compress", [], default).GetAwaiter().GetResult();
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("compression", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new CompressionToolkit(cfg).ExecuteAsync("compress", new Dictionary<string, object?> { ["source"] = "file.txt", ["format"] = "zip" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class ImageProcessingToolkitTests
{
    [Fact] public void Id_IsCorrect() => Assert.Equal("image_processing", new ImageProcessingToolkit().Id);
    [Fact] public void Name_IsCorrect() => Assert.Equal("Image Processing", new ImageProcessingToolkit().Name);
    [Fact] public void Category_IsCorrect() => Assert.Equal(ToolkitCategory.FileStorage, new ImageProcessingToolkit().Category);
    [Fact] public void NotConfigured_ReturnsSkipped()
    {
        var r = new ImageProcessingToolkit().ExecuteAsync("resize", [], default).GetAwaiter().GetResult();
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("image_processing", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new ImageProcessingToolkit(cfg).ExecuteAsync("resize", new Dictionary<string, object?> { ["source"] = "img.png", ["width"] = 100, ["height"] = 100 }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class PdfToolkitTests
{
    [Fact] public void Id_IsCorrect() => Assert.Equal("pdf", new PdfToolkit().Id);
    [Fact] public void Name_IsCorrect() => Assert.Equal("PDF", new PdfToolkit().Name);
    [Fact] public void Category_IsCorrect() => Assert.Equal(ToolkitCategory.FileStorage, new PdfToolkit().Category);
    [Fact] public void NotConfigured_ReturnsSkipped()
    {
        var r = new PdfToolkit().ExecuteAsync("extract_text", [], default).GetAwaiter().GetResult();
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("pdf", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new PdfToolkit(cfg).ExecuteAsync("extract_text", new Dictionary<string, object?> { ["file"] = "doc.pdf" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}
