namespace AGXIOME.Shared.Interfaces;

/// <summary>Central registry for discovering and resolving tools by name or category. Agents query this to build their function-calling schemas before LLM inference.</summary>
public interface IToolRegistry
{
    void Register<T>() where T : IAgxiomeTool;
    void Register(IAgxiomeTool instance);
    void Unregister(string toolName);
    void RegisterForAgent(string agentId, string toolName);
    void UnregisterFromAgent(string agentId, string toolName);
    IAgxiomeTool? GetTool(string toolName);
    IReadOnlyList<IAgxiomeTool> GetAgentTools(string agentId);
    IReadOnlyList<IAgxiomeTool> GetAllTools();
    IReadOnlyList<IAgxiomeTool> FindByCategory(string category);
    System.Text.Json.Nodes.JsonArray GetToolSchemas(string agentId);
    void AddToolHook(IToolHook hook);
}
