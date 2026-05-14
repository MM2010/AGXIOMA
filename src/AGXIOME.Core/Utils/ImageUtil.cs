using System.Text.RegularExpressions;

namespace AGXIOME.Core.Utils;

/// <summary>Utilities for image processing in vision/multimodal workflows.</summary>
public static class ImageUtil
{
    private static readonly HashSet<string> ValidMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/gif", "image/webp", "image/bmp"
    };

    private static readonly Dictionary<string, string> ExtensionMimeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".gif"] = "image/gif",
        [".webp"] = "image/webp",
        [".bmp"] = "image/bmp"
    };

    /// <summary>Load an image file as a base64 string.</summary>
    public static async Task<string> LoadImageAsBase64Async(string filePath, CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Image file not found", filePath);

        var bytes = await File.ReadAllBytesAsync(filePath, ct);
        return Convert.ToBase64String(bytes);
    }

    /// <summary>Detect MIME type from a URL or file path by extension.</summary>
    public static string DetectMimeType(string path)
    {
        var ext = Path.GetExtension(path);
        return ExtensionMimeMap.TryGetValue(ext, out var mime) ? mime : "image/png";
    }

    /// <summary>Validate that a URL points to an image (by extension).</summary>
    public static bool ValidateImageUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        var ext = Path.GetExtension(uri.AbsolutePath);
        return ExtensionMimeMap.ContainsKey(ext);
    }

    /// <summary>Build an OpenAI-compatible image content part.</summary>
    public static object BuildOpenAIImageContent(string imageUrl, string? detail = "auto")
    {
        if (imageUrl.StartsWith("data:"))
        {
            // Already a data URI
            return new { type = "image_url", image_url = new { url = imageUrl, detail } };
        }

        if (imageUrl.StartsWith("http"))
        {
            return new { type = "image_url", image_url = new { url = imageUrl, detail } };
        }

        // Local file -> base64
        var mime = DetectMimeType(imageUrl);
        var base64 = File.ReadAllBytes(imageUrl);
        var dataUri = $"data:{mime};base64,{Convert.ToBase64String(base64)}";
        return new { type = "image_url", image_url = new { url = dataUri, detail } };
    }

    /// <summary>Build an Anthropic-compatible image content part.</summary>
    public static object BuildAnthropicImageContent(string imagePath)
    {
        if (imagePath.StartsWith("http"))
        {
            return new { type = "image", source = new { type = "url", url = imagePath } };
        }

        var mime = DetectMimeType(imagePath);
        var base64 = Convert.ToBase64String(File.ReadAllBytes(imagePath));
        return new { type = "image", source = new { type = "base64", media_type = mime, data = base64 } };
    }

    /// <summary>Build a Gemini-compatible image content part.</summary>
    public static object BuildGeminiImageContent(string imagePath)
    {
        if (imagePath.StartsWith("http"))
        {
            return new { inline_data = new { mime_type = DetectMimeType(imagePath), data = "" } };
        }

        var mime = DetectMimeType(imagePath);
        var base64 = Convert.ToBase64String(File.ReadAllBytes(imagePath));
        return new { inline_data = new { mime_type = mime, data = base64 } };
    }
}
