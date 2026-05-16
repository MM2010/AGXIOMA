using System.Text.Json;

namespace AgxiomaDynamicSkillLab.Engine;

public static class SkillPromotionPolicy
{
    private const string AllowedFilesystemPolicy = "readonly:generated";
    private const int MaxAllowedExecutionMs = 10_000;
    private const int MaxAllowedMemoryMb = 256;

    public static IReadOnlyList<string> Validate(ToolDescriptor tool)
    {
        var errors = new List<string>();

        if (tool.Status != ToolStatus.Executed)
            errors.Add("Only executed tools can be promoted.");

        errors.AddRange(DynamicToolSourcePolicy.Validate(tool.SourceCode, tool.Id));

        PermissionManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<PermissionManifest>(tool.ManifestJson);
        }
        catch (JsonException)
        {
            errors.Add("Permission manifest is not valid JSON.");
            return errors;
        }

        if (manifest is null)
        {
            errors.Add("Permission manifest is missing.");
            return errors;
        }

        if (!string.Equals(manifest.Id, tool.Id, StringComparison.Ordinal))
            errors.Add("Permission manifest id must match the tool id.");

        if (string.IsNullOrWhiteSpace(manifest.Version))
            errors.Add("Permission manifest version is required.");

        if (!string.Equals(manifest.Permissions.Filesystem, AllowedFilesystemPolicy, StringComparison.Ordinal))
            errors.Add("Filesystem permission exceeds the promotion policy.");

        if (manifest.Permissions.Network)
            errors.Add("Network access is not allowed for promoted skills.");

        if (manifest.Permissions.Tools.Count > 0)
            errors.Add("Tool chaining is not allowed for promoted skills.");

        if (manifest.Permissions.MaxExecutionMs <= 0 || manifest.Permissions.MaxExecutionMs > MaxAllowedExecutionMs)
            errors.Add($"MaxExecutionMs must be between 1 and {MaxAllowedExecutionMs}.");

        if (manifest.Permissions.MaxMemoryMb <= 0 || manifest.Permissions.MaxMemoryMb > MaxAllowedMemoryMb)
            errors.Add($"MaxMemoryMb must be between 1 and {MaxAllowedMemoryMb}.");

        return errors;
    }
}

public sealed class PromotionPolicyViolationException(IReadOnlyList<string> errors)
    : InvalidOperationException("Skill promotion rejected by policy.")
{
    public IReadOnlyList<string> Errors { get; } = errors;
}