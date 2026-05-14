using System.Text.Json;
using AGXIOME.DynamicRuntime.Compilation;
using AGXIOME.DynamicRuntime.Interfaces;

namespace AgxiomaDynamicSkillLab.Engine;

/// <summary>
/// Builds dynamic tool source code from a description + task context.
/// Uses AGXIOME's AgentCodeTemplate to produce ISimpleDynamicAgent-compatible code.
/// Generates permission manifest + schema alongside the source.
/// </summary>
public sealed class DynamicToolBuilder
{
    private static readonly JsonSerializerOptions _json = new() { WriteIndented = true };

    /// <summary>
    /// Generates a C# tool class suited for the given task.
    /// Returns the complete source code + a permission manifest.
    /// </summary>
    public ToolCodeBundle BuildToolCode(
        string toolId,
        string toolName,
        string description,
        string logicBody)
    {
        var ns = "AgxiomaDynamicSkillLab.Generated";
        var className = ToPascalCase(toolName);

        var source = $$"""
            using System;
            using System.Collections.Generic;
            using System.Linq;
            using System.Text;
            using System.Text.Json;
            using System.Threading;
            using System.Threading.Tasks;
            using AGXIOME.DynamicRuntime.Attributes;
            using AGXIOME.DynamicRuntime.Interfaces;

            namespace {{ns}};

            [AgxiomeAgent("{{toolId}}", "{{EscapeCs(description)}}")]
            public sealed class {{className}} : ISimpleDynamicAgent
            {
                public string AgentId => "{{toolId}}";
                public string Description => "{{EscapeCs(description)}}";

                public async Task<string> RunAsync(string input, CancellationToken ct = default)
                {
                    {{logicBody}}
                }
            }
            """;

        var manifest = new PermissionManifest(
            Id: toolId,
            Version: "1.0.0",
            Permissions: new PermissionRules(
                Filesystem: "readonly:generated",
                Network: false,
                Tools: new List<string>(),
                MaxExecutionMs: 10_000,
                MaxMemoryMb: 256
            )
        );

        var manifestJson = JsonSerializer.Serialize(manifest, _json);

        return new ToolCodeBundle(source, manifestJson, ns, className);
    }

    /// <summary>
    /// Generates a basic unit test for the tool.
    /// </summary>
    public string GenerateTest(string toolId, string className, string exampleInput, string expectedOutputHint)
    {
        return $$"""
            using Xunit;

            namespace AgxiomaDynamicSkillLab.Generated.Tests;

            public class {{className}}Tests
            {
                [Fact]
                public async Task {{className}}_Executes_Without_Error()
                {
                    var tool = new AgxiomaDynamicSkillLab.Generated.{{className}}();
                    var result = await tool.RunAsync("{{EscapeCs(exampleInput)}}");
                    Assert.NotNull(result);
                    Assert.NotEmpty(result);
                }

                [Fact]
                public async Task {{className}}_Handles_Empty_Input()
                {
                    var tool = new AgxiomaDynamicSkillLab.Generated.{{className}}();
                    var result = await tool.RunAsync(string.Empty);
                    Assert.NotNull(result);
                }
            }
            """;
    }

    private static string ToPascalCase(string name)
    {
        var parts = name.Split(['-', '_', ' '], StringSplitOptions.RemoveEmptyEntries);
        return string.Concat(parts.Select(p =>
            p.Length == 0 ? p : char.ToUpperInvariant(p[0]) + p[1..]));
    }

    private static string EscapeCs(string s) => s.Replace("\"", "\\\"").Replace("\n", "\\n");
}

public record ToolCodeBundle(
    string SourceCode,
    string ManifestJson,
    string Namespace,
    string ClassName
);
