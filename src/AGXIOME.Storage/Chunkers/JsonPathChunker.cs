using System.Text.Json;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Storage.Chunkers;

/// <summary>
/// Splits a JSON document by navigating a list of JSON Pointer paths (RFC 6901).
/// Each matched node is serialised back to a compact JSON string and becomes one chunk.
///
/// <list type="bullet">
///   <item>If <see cref="JsonPaths"/> is empty, the top-level array items or object
///         property values are used as default paths.</item>
///   <item>Each chunk carries the originating path in its <c>Metadata</c> under
///         key <c>"json_path"</c> for downstream traceability.</item>
///   <item>If the value at a path exceeds the token budget it is recursively split
///         via <see cref="RecursiveChunker"/>.</item>
///   <item>Non-JSON input falls back to <see cref="RecursiveChunker"/> gracefully.</item>
/// </list>
///
/// <para><paramref name="overlap"/> is applied as text-level tail-carry (same as other
/// chunkers) after serialisation, not at the JSON structural level.</para>
/// </summary>
public sealed class JsonPathChunker : IChunker
{
    private readonly RecursiveChunker _fallback = new();

    /// <summary>
    /// Optional list of JSON Pointer paths (e.g. <c>/items/0/text</c>) to extract.
    /// When empty, top-level children of the root object or array are used.
    /// </summary>
    public IReadOnlyList<string> JsonPaths { get; init; } = [];

    public ChunkerType ChunkerType => ChunkerType.JsonPath;

    public IReadOnlyList<DocumentChunk> Chunk(string text, int maxTokens = 512, int overlap = 64)
    {
        if (string.IsNullOrEmpty(text))
            return [new DocumentChunk(string.Empty, 0, 0, 0)];

        JsonDocument? doc;
        try
        {
            doc = JsonDocument.Parse(text);
        }
        catch (JsonException)
        {
            // Not valid JSON — delegate to recursive fallback.
            return _fallback.Chunk(text, maxTokens, overlap);
        }

        using (doc)
        {
            var segments = ExtractSegments(doc.RootElement, JsonPaths);
            if (segments.Count == 0)
                return _fallback.Chunk(text, maxTokens, overlap);

            int charBudget  = maxTokens * 4;
            int overlapChars = overlap * 4;
            var chunks = new List<DocumentChunk>();
            int index  = 0;
            int cursor = 0;

            for (int i = 0; i < segments.Count; i++)
            {
                var (path, value) = segments[i];
                string content = value;

                // Prepend overlap tail from previous chunk (text-level carry).
                if (i > 0 && overlapChars > 0)
                {
                    string prevRaw = segments[i - 1].Value;
                    string tail = prevRaw.Length > overlapChars ? prevRaw[^overlapChars..] : prevRaw;
                    content = tail + " " + content;
                }

                var meta = new Dictionary<string, object> { ["json_path"] = path };

                if (content.Length <= charBudget)
                {
                    chunks.Add(new DocumentChunk(content.Trim(), index++, cursor, cursor + value.Length, meta));
                }
                else
                {
                    // Over-budget: sub-chunk via recursive fallback.
                    var sub = _fallback.Chunk(content, maxTokens, 0);
                    foreach (var sc in sub)
                        chunks.Add(new DocumentChunk(sc.Content, index++,
                            cursor + sc.StartOffset, cursor + sc.EndOffset, meta));
                }

                cursor += value.Length + 1;
            }

            return chunks;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────

    private static List<(string Path, string Value)> ExtractSegments(
        JsonElement root, IReadOnlyList<string> paths)
    {
        var result = new List<(string, string)>();

        if (paths.Count > 0)
        {
            foreach (string path in paths)
            {
                var node = ResolvePointer(root, path);
                if (node.HasValue)
                    result.Add((path, Serialize(node.Value)));
            }
        }
        else
        {
            // Default: top-level children.
            if (root.ValueKind == JsonValueKind.Array)
            {
                int i = 0;
                foreach (var item in root.EnumerateArray())
                    result.Add(($"/{i++}", Serialize(item)));
            }
            else if (root.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in root.EnumerateObject())
                    result.Add(($"/{prop.Name}", Serialize(prop.Value)));
            }
            else
            {
                result.Add(("/", Serialize(root)));
            }
        }

        return result;
    }

    /// <summary>Resolve a JSON Pointer (RFC 6901) on a JsonElement.</summary>
    private static JsonElement? ResolvePointer(JsonElement root, string pointer)
    {
        if (string.IsNullOrWhiteSpace(pointer) || pointer == "/")
            return root;

        var tokens = pointer.TrimStart('/').Split('/');
        JsonElement current = root;

        foreach (string token in tokens)
        {
            // Unescape RFC 6901 tokens: ~1 → /, ~0 → ~
            string key = token.Replace("~1", "/").Replace("~0", "~");

            if (current.ValueKind == JsonValueKind.Object)
            {
                if (!current.TryGetProperty(key, out current))
                    return null;
            }
            else if (current.ValueKind == JsonValueKind.Array)
            {
                if (!int.TryParse(key, out int idx) || idx < 0 || idx >= current.GetArrayLength())
                    return null;
                current = current[idx];
            }
            else
            {
                return null;
            }
        }

        return current;
    }

    private static string Serialize(JsonElement el)
        => el.ValueKind == JsonValueKind.String
            ? el.GetString() ?? string.Empty
            : el.GetRawText();
}
