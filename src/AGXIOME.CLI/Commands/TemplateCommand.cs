namespace AGXIOME.CLI.Commands;

/// <summary>agxiome new — create a new AGXIOME project from template.</summary>
public sealed class TemplateCommand : ICliCommand
{
    public string Name => "new";
    public string Description => "Create a new AGXIOME agent project from template";

    public ValueTask<CliResult> ExecuteAsync(string[] args, CancellationToken ct = default)
    {
        var name = args.Length > 0 ? args[0] : "MyAgent";
        var output = ".";
        for (int i = 1; i < args.Length; i++)
        {
            if (args[i] == "--output" && i + 1 < args.Length)
                output = args[i + 1];
        }

        var templatePath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "apps", "agxioma-agent-template");
        if (!Directory.Exists(templatePath))
            templatePath = Path.Combine(Directory.GetCurrentDirectory(), "apps", "agxioma-agent-template");

        if (!Directory.Exists(templatePath))
            return ValueTask.FromResult(CliResult.Fail("Template not found. Install with: dotnet new install apps/agxioma-agent-template"));

        var targetDir = Path.Combine(output, name);
        CopyDirectory(templatePath, targetDir);
        var msg = $"Created AGXIOME agent project at: {targetDir}\n\nNext steps:\n  cd {targetDir}\n  dotnet restore\n  dotnet run";
        return ValueTask.FromResult(CliResult.Ok(msg));
    }

    private static void CopyDirectory(string source, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (var file in Directory.GetFiles(source))
        {
            var destFile = Path.Combine(dest, Path.GetFileName(file));
            File.Copy(file, destFile, overwrite: true);
        }
        foreach (var dir in Directory.GetDirectories(source))
        {
            var destDir = Path.Combine(dest, Path.GetFileName(dir));
            CopyDirectory(dir, destDir);
        }
    }
}
