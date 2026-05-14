using AGXIOME.Shared.Models;
using System.Reflection;

namespace AGXIOME.Shared.Interfaces;

/// <summary>Abstract base for typed tools with auto JSON schema generation.</summary>
public abstract class AgxiomeFunction : IAgxiomeTool, IAgxiomeExecutableTool
{
    public abstract string Name { get; }
    public abstract string Description { get; }
    public virtual string? Version => "1.0.0";
    public virtual IReadOnlyList<string> Categories => Array.Empty<string>();
    public abstract Task<ToolResult> ExecuteAsync(ToolContext context, CancellationToken ct);

    public System.Text.Json.Nodes.JsonObject ToJsonSchema()
    {
        // Reflection-based schema generation from [ToolParameter] props
        var schema = new System.Text.Json.Nodes.JsonObject
        {
            ["name"] = Name,
            ["description"] = Description,
            ["parameters"] = GenerateParameterSchema()
        };
        return schema;
    }

    private System.Text.Json.Nodes.JsonObject GenerateParameterSchema()
    {
        var properties = new System.Text.Json.Nodes.JsonObject();
        var required = new System.Text.Json.Nodes.JsonArray();

        foreach (var prop in GetType().GetProperties())
        {
            var attr = prop.GetCustomAttribute<ToolParameterAttribute>();
            if (attr == null) continue;

            var ps = new System.Text.Json.Nodes.JsonObject
            {
                ["type"] = attr.Type ?? MapType(prop.PropertyType),
                ["description"] = attr.Description
            };
            properties[ToSnakeCase(prop.Name)] = ps;
            if (attr.Required) required.Add(ToSnakeCase(prop.Name));
        }

        return new System.Text.Json.Nodes.JsonObject
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = required
        };
    }

    private static string MapType(Type t) => t switch
    {
        { } when t == typeof(string) => "string",
        { } when t == typeof(int) || t == typeof(long) => "integer",
        { } when t == typeof(double) || t == typeof(float) => "number",
        { } when t == typeof(bool) => "boolean",
        _ => "string"
    };

    private static string ToSnakeCase(string s) =>
        string.Concat(s.Select((c, i) => i > 0 && char.IsUpper(c) ? "_" + c.ToString().ToLower() : c.ToString().ToLower()));
}

[AttributeUsage(AttributeTargets.Property)]
public class ToolParameterAttribute : Attribute
{
    public bool Required { get; set; }
    public string Description { get; set; } = "";
    public object? DefaultValue { get; set; }
    public string? Type { get; set; }
    public object? MinValue { get; set; }
    public object? MaxValue { get; set; }
}
