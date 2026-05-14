using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Storage.Chunkers;

/// <summary>
/// Splits source code into meaningful chunks using Roslyn (Microsoft.CodeAnalysis).
/// Each top-level member (class, struct, interface, record, method, property, field,
/// enum, delegate) becomes its own chunk. Nested members are included within their
/// parent chunk so each chunk is syntactically self-contained.
///
/// If the Roslyn assembly is not available at runtime (e.g. the project does not
/// reference it), the chunker falls back to <see cref="RecursiveChunker"/> transparently.
///
/// Design note: we use reflection-based late binding so that AGXIOME.Storage does not
/// take a hard compile-time dependency on Microsoft.CodeAnalysis.CSharp. Consumers that
/// want full Roslyn support should add the NuGet reference themselves.
/// </summary>
public sealed class CodeChunker : IChunker
{
    private readonly RecursiveChunker _fallback = new();

    public ChunkerType ChunkerType => ChunkerType.Code;

    public IReadOnlyList<DocumentChunk> Chunk(string text, int maxTokens = 512, int overlap = 64)
    {
        if (string.IsNullOrEmpty(text))
            return [new DocumentChunk(string.Empty, 0, 0, 0)];

        // Try Roslyn path first; gracefully fall back if unavailable.
        var roslynChunks = TryRoslynChunk(text);
        if (roslynChunks is not null && roslynChunks.Count > 0)
            return roslynChunks;

        // Fallback: heuristic line-based split on code-level delimiters.
        return HeuristicCodeChunk(text, maxTokens, overlap);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Roslyn path (reflection-based to avoid hard compile dependency)
    // ─────────────────────────────────────────────────────────────────────────

    private static IReadOnlyList<DocumentChunk>? TryRoslynChunk(string text)
    {
        try
        {
            // Dynamically resolve Microsoft.CodeAnalysis.CSharp if available.
            var csharpType = Type.GetType(
                "Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree, Microsoft.CodeAnalysis.CSharp");
            if (csharpType is null) return null;

            var parseTextMethod = csharpType.GetMethod("ParseText",
                [typeof(string), null!, null!, null!, typeof(System.Threading.CancellationToken)]);
            if (parseTextMethod is null) return null;

            dynamic? tree = parseTextMethod.Invoke(null,
                [text, null, null, null, System.Threading.CancellationToken.None]);
            if (tree is null) return null;

            dynamic root = tree.GetRoot();

            // Collect all top-level member declarations.
            var memberType = Type.GetType(
                "Microsoft.CodeAnalysis.CSharp.Syntax.MemberDeclarationSyntax, Microsoft.CodeAnalysis.CSharp");
            if (memberType is null) return null;

            var members = ((IEnumerable<dynamic>)root.DescendantNodes())
                .Where(n => memberType.IsInstanceOfType(n))
                .Where(n => IsTopLevel(n))
                .ToList();

            if (members.Count == 0) return null;

            var chunks = new List<DocumentChunk>(members.Count);
            int index = 0;
            foreach (dynamic member in members)
            {
                string content = member.ToFullString();
                if (string.IsNullOrWhiteSpace(content)) continue;

                int start = (int)member.FullSpan.Start;
                int end   = (int)member.FullSpan.End;
                chunks.Add(new DocumentChunk(content.Trim(), index++, start, end));
            }
            return chunks;
        }
        catch
        {
            return null;
        }
    }

    private static bool IsTopLevel(dynamic node)
    {
        try
        {
            // A member is "top-level" when its parent is a compilation unit or namespace.
            string parentKind = node.Parent?.Kind()?.ToString() ?? "";
            return parentKind is "CompilationUnit" or "NamespaceDeclaration"
                                 or "FileScopedNamespaceDeclaration";
        }
        catch { return false; }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Heuristic fallback: split on class/method/function-level keywords
    // ─────────────────────────────────────────────────────────────────────────

    private IReadOnlyList<DocumentChunk> HeuristicCodeChunk(
        string text, int maxTokens, int overlap)
    {
        // Keywords that typically start a new top-level code block.
        var triggers = new[] { "\npublic ", "\nprivate ", "\nprotected ", "\ninternal ",
                                "\nclass ", "\nstruct ", "\ninterface ", "\nenum ",
                                "\nrecord ", "\ndef ", "\nfunc ", "\nfunction " };

        var segments = new List<string>();
        int pos = 0;
        int prev = 0;

        while (pos < text.Length)
        {
            int next = text.Length;
            foreach (string t in triggers)
            {
                int idx = text.IndexOf(t, pos + 1, StringComparison.Ordinal);
                if (idx >= 0 && idx < next)
                    next = idx;
            }

            if (next == text.Length)
            {
                segments.Add(text[prev..].Trim());
                break;
            }

            string seg = text[prev..next].Trim();
            if (!string.IsNullOrWhiteSpace(seg))
                segments.Add(seg);

            prev = next;
            pos  = next;
        }

        if (segments.Count == 0)
            return _fallback.Chunk(text, maxTokens, overlap);

        // Subdivide over-budget segments.
        int charBudget = maxTokens * 4;
        var result = new List<DocumentChunk>();
        int chunkIndex = 0;
        int cursor = 0;

        foreach (string seg in segments)
        {
            if (seg.Length <= charBudget)
            {
                result.Add(new DocumentChunk(seg, chunkIndex++, cursor, cursor + seg.Length));
                cursor += seg.Length + 1;
            }
            else
            {
                var sub = _fallback.Chunk(seg, maxTokens, overlap);
                foreach (var sc in sub)
                    result.Add(new DocumentChunk(sc.Content, chunkIndex++,
                        cursor + sc.StartOffset, cursor + sc.EndOffset));
                cursor += seg.Length + 1;
            }
        }

        return result;
    }
}
