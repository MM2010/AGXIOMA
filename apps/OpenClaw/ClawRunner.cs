// OpenClaw — ClawRunner
// Builds an AGXIOME agent wired with a ClawSkill and executes a prompt.
// Keeps LLM I/O out of Program.cs so tests can exercise ClawRunner directly.
using AGXIOME.Core;
using AGXIOME.Shared.Config;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using OpenClaw.Skills;

namespace OpenClaw;

/// <summary>
/// Wires an AGXIOME agent with a <see cref="ClawSkill"/> and runs a prompt.
/// Strategy:
///   1. Build agent via AgxiomeBuilder, applying the skill's system-prompt extension.
///   2. Register each skill tool with the agent.
///   3. Run the user prompt; return the text response.
/// </summary>
public sealed class ClawRunner
{
    private readonly IAgxiomeAgent _agent;

    /// <param name="agent">Pre-built AGXIOME agent. Caller owns disposal.</param>
    public ClawRunner(IAgxiomeAgent agent)
    {
        _agent = agent;
    }

    /// <summary>
    /// Applies a <see cref="ClawSkill"/> to the agent (tools + system-prompt extension)
    /// and runs a single-turn prompt.
    /// </summary>
    public async Task<string> RunAsync(
        ClawSkill skill,
        string    prompt,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(skill);
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

        // Register every tool bundled in the skill.
        foreach (var tool in skill.Tools)
            _agent.RegisterTool(tool);

        var request = new AgentRequest(
            Prompt: $"{skill.SystemPromptExtension}\n\n---\n\n{prompt}"
        );

        var response = await _agent.RunAsync(request, ct);
        return response.Content ?? string.Empty;
    }

    /// <summary>
    /// Runs a raw prompt without applying a skill.
    /// Useful for conversational chat mode.
    /// </summary>
    public async Task<string> ChatAsync(string prompt, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        var response = await _agent.RunAsync(new AgentRequest(Prompt: prompt), ct);
        return response.Content ?? string.Empty;
    }
}
