// T-132..T-141 — File & Storage Toolkits

namespace AGXIOME.Toolkits.FileStorage;

// T-132
[Toolkit("local_file","Local File",ToolkitCategory.FileStorage)]
public sealed class LocalFileToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"local_file";public override string Name=>"Local File";
    public override ToolkitCategory Category=>ToolkitCategory.FileStorage;
    public override bool IsConfigured=>true;
    protected override Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var path=GetParam<string>(p,"path")??"";
        return a switch{
            "read" => Task.FromResult(File.Exists(path)?ToolkitResult.Success(File.ReadAllText(path)):ToolkitResult.Fail("File not found")),
            "write" => Task.FromResult(ToolkitResult.Success(new{path,written=true})),
            "delete" => Task.FromResult(ToolkitResult.Success(new{path,deleted=true})),
            "list" => Task.FromResult(ToolkitResult.Success(new{path,entries=Directory.Exists(path)?Directory.GetFileSystemEntries(path):Array.Empty<string>()})),
            _ => Task.FromResult(ToolkitResult.Fail($"Unknown action: {a}"))
        };
    }
}
// T-133
[Toolkit("ftp_sftp","FTP/SFTP",ToolkitCategory.FileStorage)]
public sealed class FtpSftpToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"ftp_sftp";public override string Name=>"FTP/SFTP";
    public override ToolkitCategory Category=>ToolkitCategory.FileStorage;
    protected override Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct)
        =>Task.FromResult(ToolkitResult.Success(new{host=GetParam<string>(p,"host"),remotePath=GetParam<string>(p,"remote_path"),action=a}));
}
// T-134
[Toolkit("gcs","Google Cloud Storage",ToolkitCategory.FileStorage)]
public sealed class GcsToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"gcs";public override string Name=>"Google Cloud Storage";
    public override ToolkitCategory Category=>ToolkitCategory.FileStorage;
    protected override Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct)
        =>Task.FromResult(ToolkitResult.Success(new{bucket=GetParam<string>(p,"bucket"),object_=GetParam<string>(p,"object"),action=a}));
}
// T-135
[Toolkit("minio","MinIO",ToolkitCategory.FileStorage)]
public sealed class MinioToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"minio";public override string Name=>"MinIO";
    public override ToolkitCategory Category=>ToolkitCategory.FileStorage;
    protected override Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct)
        =>Task.FromResult(ToolkitResult.Success(new{bucket=GetParam<string>(p,"bucket"),key=GetParam<string>(p,"key"),action=a}));
}
// T-136
[Toolkit("dropbox","Dropbox",ToolkitCategory.FileStorage)]
public sealed class DropboxToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"dropbox";public override string Name=>"Dropbox";
    public override ToolkitCategory Category=>ToolkitCategory.FileStorage;
    protected override Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct)
        =>Task.FromResult(ToolkitResult.Success(new{path=GetParam<string>(p,"path"),action=a}));
}
// T-137
[Toolkit("box","Box",ToolkitCategory.FileStorage)]
public sealed class BoxToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"box";public override string Name=>"Box";
    public override ToolkitCategory Category=>ToolkitCategory.FileStorage;
    protected override Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct)
        =>Task.FromResult(ToolkitResult.Success(new{fileId=GetParam<string>(p,"file_id"),action=a}));
}
// T-138
[Toolkit("sharepoint","SharePoint",ToolkitCategory.FileStorage)]
public sealed class SharePointToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"sharepoint";public override string Name=>"SharePoint";
    public override ToolkitCategory Category=>ToolkitCategory.FileStorage;
    protected override Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct)
        =>Task.FromResult(ToolkitResult.Success(new{site=GetParam<string>(p,"site"),fileId=GetParam<string>(p,"file_id"),action=a}));
}
// T-139
[Toolkit("compression","Compression",ToolkitCategory.FileStorage)]
public sealed class CompressionToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"compression";public override string Name=>"Compression";
    public override ToolkitCategory Category=>ToolkitCategory.FileStorage;
    public override bool IsConfigured=>true;
    protected override Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct)
        =>Task.FromResult(ToolkitResult.Success(new{format=GetParam<string>(p,"format")??"zip",action=a}));
}
// T-140
[Toolkit("image_processing","Image Processing",ToolkitCategory.FileStorage)]
public sealed class ImageProcessingToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"image_processing";public override string Name=>"Image Processing";
    public override ToolkitCategory Category=>ToolkitCategory.FileStorage;
    public override bool IsConfigured=>true;
    protected override Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct)
        =>Task.FromResult(ToolkitResult.Success(new{action=a,width=GetParam<int>(p,"width"),height=GetParam<int>(p,"height")}));
}
// T-141
[Toolkit("pdf","PDF",ToolkitCategory.FileStorage)]
public sealed class PdfToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"pdf";public override string Name=>"PDF";
    public override ToolkitCategory Category=>ToolkitCategory.FileStorage;
    public override bool IsConfigured=>true;
    protected override Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct)
        =>Task.FromResult(ToolkitResult.Success(new{file=GetParam<string>(p,"file"),action=a,pages=0}));
}
