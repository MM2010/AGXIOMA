// OpenClaw.Skills — Skill definitions
// A ClawSkill bundles a system-prompt extension with a set of executable tools.
// Minimal dependency: AGXIOME.Shared + OpenClaw.Tools only.
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using OpenClaw.Tools;

namespace OpenClaw.Skills;

/// <summary>
/// Lightweight skill descriptor: a named collection of tools plus a system-prompt extension.
/// Applied to an agent via <see cref="IAgxiomeAgent.RegisterTool"/> + system prompt injection.
/// </summary>
public sealed record ClawSkill(
    string Id,
    string Name,
    string Description,
    string SystemPromptExtension,
    IReadOnlyList<IAgxiomeExecutableTool> Tools
);

/// <summary>
/// Code review skill — equips the agent with FileSystemTool and CodeSearchTool
/// and injects a structured code-review prompt directive.
/// </summary>
public static class CodeReviewSkill
{
    public static ClawSkill Create(string rootPath) => new(
        Id:   "code-review",
        Name: "Code Review",
        Description: "Reviews code for quality, security, and best-practice issues.",
        SystemPromptExtension: """
            You are a senior software engineer performing a thorough code review.
            For every file you review, consider:
            1. Correctness — logic errors, edge cases, off-by-one.
            2. Security — injection, path traversal, unvalidated input, secrets in code.
            3. Performance — hot-path allocations, LINQ abuse, blocking I/O.
            4. Maintainability — naming, cohesion, SOLID violations, dead code.
            5. Test coverage — missing assertions, flakey patterns, fictional tests.

            Use the file_system tool to read files and code_search to find patterns.
            Always cite the exact file path and line number in your findings.
            Format output as:
              [SEVERITY: INFO|WARN|ERROR] file:line — finding description.
            """,
        Tools: [
            new FileSystemTool(rootPath),
            new CodeSearchTool(rootPath)
        ]
    );
}

/// <summary>
/// Project analysis skill — provides a structural overview of a codebase:
/// project layout, dependency graph summary, and key metrics.
/// </summary>
public static class ProjectAnalysisSkill
{
    public static ClawSkill Create(string rootPath) => new(
        Id:   "project-analysis",
        Name: "Project Analysis",
        Description: "Analyses project structure, dependencies, and metrics.",
        SystemPromptExtension: """
            You are an expert software architect analysing a codebase.
            Your tasks:
            1. Map the directory structure using file_system list actions.
            2. Identify project/solution files (.csproj, .sln, package.json, pyproject.toml, etc.).
            3. Identify entry points (Program.cs, index.ts, main.py, etc.).
            4. Summarise: language breakdown, dependency count, approximate LOC, layers.
            5. Flag structural concerns: circular dependencies, monolithic files, missing tests.

            Use file_system and code_search tools systematically.
            Present results as a structured markdown report with sections:
            ## Overview, ## Structure, ## Dependencies, ## Metrics, ## Concerns.
            """,
        Tools: [
            new FileSystemTool(rootPath),
            new CodeSearchTool(rootPath)
        ]
    );
}
