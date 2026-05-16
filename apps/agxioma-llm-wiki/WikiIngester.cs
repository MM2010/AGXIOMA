using AGXIOME.Storage.Embedders;
using System.Security.Cryptography;
using System.Text;

namespace AgxiomaLLMWiki;

/// <summary>
/// Scans the AGXIOMA project, reads source files, chunks + embeds them into
/// PostgreSQL pgvector, and delegates wiki page generation to WikiPageWriter.
///
/// Karpathy LLMWiki pattern:
///   Raw sources (immutable) → chunk → embed → pgvector
///                           → LLM synthesis → wiki pages (markdown)
/// </summary>
public sealed class WikiIngester : IWikiIngester
{
    private const int MaxIngestedFilesPerRun = 256;
    private const int MaxIngestedDirectoryDepth = 10;
    private readonly WikiSettings          _settings;
    private readonly IIngestProfileProvider _profileProvider;
    private readonly WikiDatabase          _db;
    private readonly WikiPageWriter        _pageWriter;
    private readonly OllamaMxbaiEmbedder   _embedder;
    private readonly ILogger<WikiIngester> _log;

    // Prevents multiple concurrent ingest jobs on the same singleton instance
    private readonly SemaphoreSlim _ingestLock = new(1, 1);

    // Extensions used for page category detection
    private static readonly HashSet<string> CodeExts   = [".cs", ".csproj", ".props"];
    private static readonly HashSet<string> DocExts    = [".md", ".txt"];
    private static readonly HashSet<string> ConfigExts = [".json", ".yml", ".yaml"];

    public WikiIngester(
        WikiSettings settings,
        IIngestProfileProvider profileProvider,
        WikiDatabase db,
        WikiPageWriter pageWriter,
        OllamaMxbaiEmbedder embedder,
        ILogger<WikiIngester> log)
    {
        _settings        = settings;
        _profileProvider = profileProvider;
        _db              = db;
        _pageWriter      = pageWriter;
        _embedder        = embedder;
        _log             = log;
    }

    // ── Public entry point ────────────────────────────────────────────────────

    /// <summary>
    /// Ingests all matching project files. Updates job state via Redis.
    /// Returns summary counts.
    /// </summary>
    public async Task RunAsync(
        IngestJobState job,
        string?        subPath = null,
        bool           force   = false,
        CancellationToken ct   = default)
    {
        // Guard: only one ingest at a time (singleton instance, multiple /ingest calls would race)
        if (!await _ingestLock.WaitAsync(TimeSpan.Zero, ct))
        {
            job.Status       = "error";
            job.ErrorMessage = "Another ingest job is already running. Wait for it to complete.";
            await _db.SaveJobStateAsync(job);
            _log.LogWarning("[Ingester] Rejected concurrent ingest request (job {Id})", job.JobId);
            return;
        }
        try
        {
        await RunInternalAsync(job, subPath, force, ct);
        }
        finally
        {
            _ingestLock.Release();
        }
    }

    private async Task RunInternalAsync(
        IngestJobState job,
        string?        subPath,
        bool           force,
        CancellationToken ct)
    {
        // Load the active profile — may come from appsettings, DB, API, etc.
        var profile = await _profileProvider.GetProfileAsync(ct);

        var rootPath = string.IsNullOrWhiteSpace(subPath)
            ? _settings.ProjectRootPath
            : Path.Combine(_settings.ProjectRootPath, subPath);

        rootPath = Path.GetFullPath(rootPath);
        var projectRoot = Path.GetFullPath(_settings.ProjectRootPath);
        if (!IsUnderRoot(projectRoot, rootPath))
        {
            job.Status       = "error";
            job.ErrorMessage = $"Path '{subPath}' is outside the configured project root.";
            await _db.SaveJobStateAsync(job);
            return;
        }

        if (ContainsReparsePoint(rootPath, projectRoot))
        {
            job.Status       = "error";
            job.ErrorMessage = "Requested path contains a reparse point and cannot be ingested.";
            await _db.SaveJobStateAsync(job);
            return;
        }

        if (!Directory.Exists(rootPath))
        {
            job.Status       = "error";
            job.ErrorMessage = string.IsNullOrWhiteSpace(subPath)
                ? "Configured project root was not found."
                : $"Path '{subPath}' was not found under the configured project root.";
            await _db.SaveJobStateAsync(job);
            return;
        }

        // Use fixed char limit from profile (probe-based detection is unreliable:
        // BPE tokenizers compress repeated 'a' chars, causing overestimation for real code).
        // mxbai-embed-large: 512 token context. C# ≈ 3.5 chars/token → 1200 chars ≈ 340 tokens.
        var effectiveMaxChars = profile.EmbedMaxChars;
        _log.LogInformation("[Ingester] Embedder max input (from profile): {MaxChars} chars", effectiveMaxChars);

        var files = EnumerateSourceFiles(rootPath, profile);
        if (files.Count > MaxIngestedFilesPerRun)
        {
            job.Status       = "error";
            job.ErrorMessage = $"Requested path contains too many files. Max allowed: {MaxIngestedFilesPerRun}.";
            await _db.SaveJobStateAsync(job);
            return;
        }

        job.FilesTotal = files.Count;
        job.Status     = "running";
        await _db.SaveJobStateAsync(job);

        var parallelism = Math.Max(1, profile.IngestParallelism);
        _log.LogInformation("[Ingester] Starting ingest of {Count} files from {Root} (parallelism={P})",
            files.Count, rootPath, parallelism);

        // Thread-safe counters for parallel processing
        var pCreated   = 0;
        var pUpdated   = 0;
        var pProcessed = 0;
        var saveLock   = new SemaphoreSlim(1, 1);

        await Parallel.ForEachAsync(
            files,
            new ParallelOptions { MaxDegreeOfParallelism = parallelism, CancellationToken = ct },
            async (filePath, innerCt) =>
            {
                var relativePath = Path.GetRelativePath(_settings.ProjectRootPath, filePath)
                                       .Replace('\\', '/');
                job.CurrentFile = relativePath; // benign race — display only

                try
                {
                    var result = await IngestFileAsync(filePath, relativePath, force, profile, innerCt);
                    if      (result == IngestResult.Created) Interlocked.Increment(ref pCreated);
                    else if (result == IngestResult.Updated) Interlocked.Increment(ref pUpdated);
                }
                catch (Exception ex)
                {
                    _log.LogWarning("[Ingester] Skipped {File}: {Error}", relativePath, ex.Message);
                }

                var processed = Interlocked.Increment(ref pProcessed);
                job.FilesProcessed = processed;

                if (processed % 10 == 0)
                {
                    await saveLock.WaitAsync(innerCt);
                    try
                    {
                        job.PagesCreated = pCreated;
                        job.PagesUpdated = pUpdated;
                        await _db.SaveJobStateAsync(job);
                    }
                    finally { saveLock.Release(); }
                }
            });

        job.PagesCreated   = pCreated;
        job.PagesUpdated   = pUpdated;
        job.FilesProcessed = pProcessed;

        job.Status      = "done";
        job.CurrentFile = null;
        await _db.SaveJobStateAsync(job);

        // Regenerate index.md and append to log
        await _pageWriter.RegenerateIndexAsync(ct);
        await _pageWriter.AppendToLogMdAsync(
            "ingest",
            $"Ingest completed — {files.Count} files processed, " +
            $"{job.PagesCreated} pages created, {job.PagesUpdated} pages updated",
            ct);

        await _db.AppendLogAsync("ingest",
            $"Full ingest — {files.Count} files, {job.PagesCreated}c {job.PagesUpdated}u",
            $"SubPath={subPath ?? "root"}, Force={force}",
            ct);

        _log.LogInformation("[Ingester] Done: {Created} created, {Updated} updated",
            job.PagesCreated, job.PagesUpdated);
    }

    private static bool IsUnderRoot(string root, string fullPath)
    {
        var normalizedRoot = EnsureTrailingSeparator(root);
        var normalizedPath = EnsureTrailingSeparator(fullPath);
        return normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase)
            || string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase);
    }

    private static string EnsureTrailingSeparator(string path)
    {
        var fullPath = Path.GetFullPath(path);
        return fullPath.EndsWith(Path.DirectorySeparatorChar)
            ? fullPath
            : fullPath + Path.DirectorySeparatorChar;
    }

    // ── Per-file ingestion ────────────────────────────────────────────────────

    private async Task<IngestResult> IngestFileAsync(
        string        filePath,
        string        relativePath,
        bool          force,
        IngestProfile profile,
        CancellationToken ct)
    {
        var fileInfo = new FileInfo(filePath);

        // Guard: check size BEFORE reading to avoid OOM on large files (e.g. scraper output dumps)
        if (fileInfo.Length > 500_000)
        {
            _log.LogDebug("[Ingester] Skipped large file ({Size:N0} bytes): {File}", fileInfo.Length, relativePath);
            return IngestResult.Skipped;
        }

        var content = await File.ReadAllTextAsync(filePath, Encoding.UTF8, ct);

        // Skip empty or binary-like files
        if (string.IsNullOrWhiteSpace(content))
            return IngestResult.Skipped;

        // Normalize raw content: collapse excess blank lines before chunking
        content = NormalizeContent(content);

        var hash    = ComputeHash(content);
        var fileType = GetFileType(filePath);

        var existing = await _db.GetSourceByPathAsync(relativePath, ct);
        if (!force && existing?.ContentHash == hash)
            return IngestResult.Skipped;  // unchanged

        // Chunk and embed the raw source
        var chunks     = ChunkText(content, profile.EmbedMaxChars, profile.ChunkOverlap * 4);
        var pageSlug   = DerivePageSlug(relativePath, fileType);

        // Generate wiki page via LLM
        var isNew    = existing is null;
        var pageResult = await _pageWriter.GenerateOrUpdatePageAsync(
            pageSlug, relativePath, fileType, content, isNew, ct);

        // Embed chunks and store in pgvector (keyed to pageSlug)
        await _db.DeleteEmbeddingsBySlugAsync(pageSlug, ct);
        var chunkTexts = chunks.ToList();
        var embedTexts = chunkTexts.Select(NormalizeForEmbedding).ToList();

        // Embed one chunk at a time to avoid Ollama batch-size limits (400 on large files)
        var embeddings = new float[chunkTexts.Count][];
        for (int i = 0; i < embedTexts.Count; i++)
        {
            try
            {
                var single = await _embedder.EmbedBatchAsync([embedTexts[i]], ct);
                embeddings[i] = single.Length > 0 && single[0] != null
                    ? single[0]
                    : Array.Empty<float>();
            }
            catch (Exception ex)
            {
                _log.LogDebug("[Ingester] Chunk {I} embed failed ({File}): {Error}", i, relativePath, ex.Message);
                embeddings[i] = Array.Empty<float>();
            }
        }

        _log.LogDebug("[Ingester] Embedded {ChunkCount} chunks for {File}",
            embedTexts.Count, relativePath);

        for (int i = 0; i < chunkTexts.Count; i++)
        {
            if (embeddings[i] == null || embeddings[i].Length == 0) continue; // skip failed chunks

            var chunkId = $"{pageSlug}:{i}";
            await _db.UpsertEmbeddingAsync(
                id:          chunkId,
                pageSlug:    pageSlug,
                vector:      embeddings[i],
                textContent: chunkTexts[i],
                metadata:    new Dictionary<string, string>
                {
                    ["relative_path"] = relativePath,
                    ["file_type"]     = fileType,
                    ["chunk_index"]   = i.ToString()
                },
                ct: ct);
        }

        // Track source in database
        await _db.UpsertSourceAsync(new WikiSource
        {
            RelativePath   = relativePath,
            FullPath       = filePath,
            FileType       = fileType,
            ContentHash    = hash,
            FileSizeBytes  = fileInfo.Length
        }, ct);

        // Invalidate Redis cache for this page
        await _db.InvalidatePageCacheAsync(pageSlug);

        _log.LogDebug("[Ingester] {Result} {RelPath} → {Slug} ({Chunks} chunks)",
            isNew ? "Created" : "Updated", relativePath, pageSlug, chunkTexts.Count);

        return isNew ? IngestResult.Created : IngestResult.Updated;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private List<string> EnumerateSourceFiles(string rootPath, IngestProfile profile)
    {
        var extSet     = new HashSet<string>(profile.SourceExtensions, StringComparer.OrdinalIgnoreCase);
        var excludeSet = new HashSet<string>(profile.SourceExcludes,   StringComparer.OrdinalIgnoreCase);
        var testFolder = Path.GetFileName(_settings.WikiOutputPath.TrimEnd(Path.DirectorySeparatorChar,
                                                                             Path.AltDirectorySeparatorChar));

        return Directory.EnumerateFiles(rootPath, "*.*", SearchOption.AllDirectories)
            .Where(f =>
            {
                if (!IsUnderRoot(_settings.ProjectRootPath, f))
                    return false;

                if (ContainsReparsePoint(f, _settings.ProjectRootPath))
                    return false;

                if (GetRelativeDepth(rootPath, f) > MaxIngestedDirectoryDepth)
                    return false;

                var parts = f.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]);
                // Exclude any path segment matching the exclusion list or wiki output dir
                if (parts.Any(p => excludeSet.Contains(p, StringComparer.OrdinalIgnoreCase)))
                    return false;
                if (!string.IsNullOrEmpty(testFolder) &&
                    parts.Any(p => p.Equals(testFolder, StringComparison.OrdinalIgnoreCase)))
                    return false;
                return extSet.Contains(Path.GetExtension(f));
            })
            .OrderBy(f => f)
            .ToList();
    }

    private static string ComputeHash(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes)[..16];
    }

    private static string GetFileType(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".cs"     => "csharp",
            ".csproj" => "project",
            ".props"  => "props",
            ".md"     => "markdown",
            ".json"   => "json",
            ".yml" or ".yaml" => "yaml",
            ".txt"    => "text",
            _         => "other"
        };
    }

    /// <summary>Derives a wiki page slug from the source file relative path.</summary>
    private static string DerivePageSlug(string relativePath, string fileType)
    {
        // Remove extension, normalise path separators
        var noExt = Path.ChangeExtension(relativePath, null).Replace('\\', '/');

        // Categorise into entities/concepts/sources
        if (fileType is "csharp" or "project" or "props")
        {
            // Try to detect entity-level names (short file names → entities)
            var fileName = Path.GetFileNameWithoutExtension(relativePath);
            if (fileName.Length < 40 && !fileName.Contains('.'))
                return $"entities/{Slugify(fileName)}";
            return $"sources/{Slugify(noExt.Replace("/", "-"))}";
        }

        if (fileType is "markdown" or "text")
        {
            var fileName = Path.GetFileNameWithoutExtension(relativePath);
            // PLAN, ARCHITECT, docs → concepts
            if (relativePath.Contains("PLAN/") || relativePath.Contains("ARCHITECT") ||
                relativePath.Contains("docs/"))
                return $"concepts/{Slugify(fileName)}";
            return $"sources/{Slugify(fileName)}";
        }

        return $"sources/{Slugify(Path.GetFileNameWithoutExtension(relativePath))}";
    }

    private static string Slugify(string s) =>
        s.ToLowerInvariant()
         .Replace(' ', '-')
         .Replace('_', '-')
         .Replace('.', '-')
         .Trim('-');

    /// <summary>
    /// Simple recursive text chunker that respects paragraph / line boundaries.
    /// Returns overlapping chunks of at most maxTokens approximate tokens
    /// (1 token ≈ 4 chars for English / C# source).
    /// </summary>
    // ── Embedder warmup ──────────────────────────────────────────────────────

    /// <summary>
    /// Probes the embedder's practical maximum input length using a latency-based
    /// heuristic. Ollama models silently truncate long inputs instead of raising
    /// HTTP errors, so the probe measures the baseline latency for a short string,
    /// then finds the largest size whose latency stays within a 4× multiplier of
    /// the baseline. Candidates tested: 256 → 512 → 1024 → 1536 → 2048 chars.
    /// Returns a value with a 10% safety margin applied.
    /// </summary>
    private async Task<int> ProbeEmbedMaxCharsAsync(CancellationToken ct)
    {
        // Candidate sizes to test (chars)
        int[] candidates = [256, 512, 1024, 1536, 2048];
        const double MaxLatencyMultiplier = 4.0;
        const int    FallbackChars        = 1024;

        // ── Baseline: measure latency for a short warm-up probe ──────────────
        var warmup = new string('a', candidates[0]);
        long baselineMs;
        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            await _embedder.EmbedAsync(warmup, ct);
            sw.Stop();
            baselineMs = Math.Max(sw.ElapsedMilliseconds, 100); // avoid division by zero
        }
        catch
        {
            _log.LogWarning("[Ingester] Embedder warmup failed — using {Fallback} chars", FallbackChars);
            return FallbackChars;
        }

        _log.LogInformation("[Ingester] Embedder baseline ({Chars} chars): {Ms}ms", candidates[0], baselineMs);

        // ── Step through candidates until latency budget exceeded ─────────────
        int safeSize = candidates[0];
        foreach (var size in candidates.Skip(1))
        {
            var probe = new string('a', size);
            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                await _embedder.EmbedAsync(probe, ct);
                sw.Stop();

                double ratio = (double)sw.ElapsedMilliseconds / baselineMs;
                _log.LogInformation("[Ingester] Probe {Size} chars: {Ms}ms (×{Ratio:F1} baseline)",
                    size, sw.ElapsedMilliseconds, ratio);

                if (ratio > MaxLatencyMultiplier)
                {
                    _log.LogInformation("[Ingester] Latency exceeded {Max}× at {Size} — stopping probe", MaxLatencyMultiplier, size);
                    break;
                }

                safeSize = size;
            }
            catch
            {
                _log.LogInformation("[Ingester] Probe failed at {Size} chars — stopping probe", size);
                break;
            }
        }

        int result = (int)(safeSize * 0.9);
        _log.LogInformation("[Ingester] Effective max input: {SafeSize} chars (−10% from {RawSize})", result, safeSize);
        return result;
    }

    private static IReadOnlyList<string> ChunkText(string text, int maxChars, int overlapChars)
    {
        int overlapCh = overlapChars;

        var paragraphs = text.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries);
        var chunks     = new List<string>();
        var buffer     = new StringBuilder();

        foreach (var para in paragraphs)
        {
            // If a single paragraph is larger than maxChars, hard-split it
            if (para.Length > maxChars)
            {
                // Flush buffer first
                if (buffer.Length > 0)
                {
                    chunks.Add(buffer.ToString().Trim());
                    buffer.Clear();
                }
                var pos = 0;
                while (pos < para.Length)
                {
                    var len = Math.Min(maxChars, para.Length - pos);
                    chunks.Add(para.Substring(pos, len).Trim());
                    var advance = len - overlapCh;
                    if (advance <= 0) break; // remaining ≤ overlap: last chunk, stop to avoid infinite loop
                    pos += advance;
                }
                continue;
            }

            if (buffer.Length + para.Length + 2 > maxChars && buffer.Length > 0)
            {
                chunks.Add(buffer.ToString().Trim());
                // Keep overlap
                var bufStr       = buffer.ToString();
                var overlapStart = Math.Max(0, bufStr.Length - overlapCh);
                buffer.Clear();
                buffer.Append(bufStr[overlapStart..]);
            }
            buffer.Append(para);
            buffer.Append("\n\n");
        }

        if (buffer.Length > 0)
            chunks.Add(buffer.ToString().Trim());

        return chunks.Count > 0 ? chunks : [text.Length > maxChars ? text[..maxChars] : text];
    }

    private static int GetRelativeDepth(string rootPath, string fullPath)
    {
        var relative = Path.GetRelativePath(rootPath, Path.GetDirectoryName(fullPath) ?? fullPath);
        if (relative == ".")
            return 0;

        return relative.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries).Length;
    }

    private static bool ContainsReparsePoint(string fullPath, string stopRoot)
    {
        var current = File.Exists(fullPath)
            ? new FileInfo(fullPath).Directory
            : new DirectoryInfo(fullPath);

        while (current is not null)
        {
            if ((current.Attributes & FileAttributes.ReparsePoint) != 0)
                return true;

            if (string.Equals(current.FullName, Path.GetFullPath(stopRoot), StringComparison.OrdinalIgnoreCase))
                break;

            current = current.Parent;
        }

        if (File.Exists(fullPath))
        {
            var fileInfo = new FileInfo(fullPath);
            return (fileInfo.Attributes & FileAttributes.ReparsePoint) != 0;
        }

        return false;
    }

    /// <summary>
    /// Normalises text before sending to the embedder:
    /// collapses all whitespace/newlines to single spaces so the model
    /// counts tokens on content rather than formatting.
    /// </summary>
    /// <summary>
    /// Light normalization of raw file content before chunking:
    /// normalizes line endings and collapses runs of 3+ blank lines to 1.
    /// Preserves single blank lines (paragraph breaks used by ChunkText).
    /// </summary>
    private static string NormalizeContent(string text)
    {
        // Normalize line endings
        text = text.Replace("\r\n", "\n").Replace('\r', '\n');
        // Collapse 3+ consecutive newlines to 2 (= one blank line)
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\n{3,}", "\n\n");
        return text.Trim();
    }

    private static string NormalizeForEmbedding(string text) =>
        System.Text.RegularExpressions.Regex.Replace(text.Replace('\r', ' ').Replace('\n', ' '), @"\s{2,}", " ").Trim();

    private enum IngestResult { Created, Updated, Skipped }
}
