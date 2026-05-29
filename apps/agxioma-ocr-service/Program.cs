using System.Diagnostics;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});

var app = builder.Build();

app.MapGet("/health", (IConfiguration configuration) => Results.Ok(new
{
    status = "ok",
    engine = "tesseract",
    defaultLanguage = configuration["Ocr:DefaultLanguage"] ?? "eng",
    utc = DateTimeOffset.UtcNow,
}));

app.MapPost("/ocr", async (HttpRequest request, IConfiguration configuration, CancellationToken ct) =>
{
    if (!request.HasFormContentType)
        return Results.BadRequest(new { error = "multipart/form-data is required" });

    var form = await request.ReadFormAsync(ct);
    var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
    if (file is null || file.Length <= 0)
        return Results.BadRequest(new { error = "file is required" });

    var maxBytes = configuration.GetValue<long?>("Ocr:MaxFileBytes") ?? 10 * 1024 * 1024;
    if (file.Length > maxBytes)
        return Results.BadRequest(new { error = $"file exceeds max size of {maxBytes} bytes" });

    var defaultLanguage = configuration["Ocr:DefaultLanguage"] ?? "eng";
    var requestedLanguage = form["language"].FirstOrDefault();
    var language = string.IsNullOrWhiteSpace(requestedLanguage) ? defaultLanguage : requestedLanguage.Trim().ToLowerInvariant();

    if (!IsSafeLanguage(language))
        return Results.BadRequest(new { error = "invalid language code" });

    var tempRoot = Path.Combine(Path.GetTempPath(), "agxioma-ocr");
    Directory.CreateDirectory(tempRoot);

    var extension = Path.GetExtension(file.FileName);
    if (string.IsNullOrWhiteSpace(extension))
        extension = ".bin";

    var tempFile = Path.Combine(tempRoot, $"{Guid.NewGuid():N}{extension}");

    try
    {
        await using (var stream = File.Create(tempFile))
        await using (var input = file.OpenReadStream())
        {
            await input.CopyToAsync(stream, ct);
        }

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = configuration["Ocr:TesseractCommand"] ?? "tesseract",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };

        process.StartInfo.ArgumentList.Add(tempFile);
        process.StartInfo.ArgumentList.Add("stdout");
        process.StartInfo.ArgumentList.Add("-l");
        process.StartInfo.ArgumentList.Add(language);

        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync(ct);
        var errorTask = process.StandardError.ReadToEndAsync(ct);

        await process.WaitForExitAsync(ct);

        var text = (await outputTask).Trim();
        var stderr = (await errorTask).Trim();

        if (process.ExitCode != 0)
        {
            return Results.Problem(
                title: "OCR execution failed",
                detail: string.IsNullOrWhiteSpace(stderr) ? "Tesseract returned a non-zero exit code." : stderr,
                statusCode: StatusCodes.Status502BadGateway);
        }

        return Results.Ok(new
        {
            text,
            language,
            bytes = file.Length,
            stderr,
        });
    }
    finally
    {
        if (File.Exists(tempFile))
            File.Delete(tempFile);
    }
});

app.Run();

static bool IsSafeLanguage(string value)
{
    if (string.IsNullOrWhiteSpace(value))
        return false;

    foreach (var ch in value)
    {
        if ((ch < 'a' || ch > 'z') && ch != '+' && ch != '_')
            return false;
    }

    return true;
}

public partial class Program;