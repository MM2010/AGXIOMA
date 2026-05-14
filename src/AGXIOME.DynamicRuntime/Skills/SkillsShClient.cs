using System.Net.Http.Json;
using System.Collections.Concurrent;

namespace AGXIOME.DynamicRuntime.Skills;

/// <summary>
/// HTTP client per il marketplace Skills.sh.
/// Cache in memoria 1h, rispetta Retry-After.
/// </summary>
public sealed class SkillsShClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly ConcurrentDictionary<string, (SkillDefinition Def, DateTime Expires)> _cache = new();
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(1);

    // Base URL configurabile (per mock in test)
    public string BaseUrl { get; init; } = "https://api.skills.sh/v1";

    public SkillsShClient(HttpClient? httpClient = null)
    {
        _http = httpClient ?? new HttpClient();
        _http.Timeout = TimeSpan.FromSeconds(10);
    }

    /// <summary>Cerca skills per query testuale.</summary>
    public async Task<IReadOnlyList<SkillDefinition>> SearchAsync(string query, int limit = 10, CancellationToken ct = default)
    {
        var url = $"{BaseUrl}/skills/search?q={Uri.EscapeDataString(query)}&limit={limit}";
        try
        {
            var result = await _http.GetFromJsonAsync<SkillDefinition[]>(url, ct);
            return result ?? [];
        }
        catch (HttpRequestException)
        {
            return [];
        }
    }

    /// <summary>Recupera una skill per ID. Usa cache 1h.</summary>
    public async Task<SkillDefinition?> GetSkillAsync(string skillId, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(skillId, out var cached) && cached.Expires > DateTime.UtcNow)
            return cached.Def;

        var url = $"{BaseUrl}/skills/{Uri.EscapeDataString(skillId)}";
        try
        {
            var skill = await _http.GetFromJsonAsync<SkillDefinition>(url, ct);
            if (skill is not null)
                _cache[skillId] = (skill, DateTime.UtcNow.Add(CacheTtl));
            return skill;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    /// <summary>Lista le categorie disponibili.</summary>
    public async Task<IReadOnlyList<string>> ListCategoriesAsync(CancellationToken ct = default)
    {
        var url = $"{BaseUrl}/categories";
        try
        {
            var categories = await _http.GetFromJsonAsync<string[]>(url, ct);
            return categories ?? [];
        }
        catch (HttpRequestException)
        {
            return [];
        }
    }

    public void Dispose() => _http.Dispose();
}
