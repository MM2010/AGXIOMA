// AGXIOME.CLI — Build, configure, and run agents.
// This is the entry point for the AGXIOME framework command-line interface.

using AGXIOME.Shared.Config;
using AGXIOME.Shared.Models;
using AGXIOME.Core.Agent;
using AGXIOME.LLMWIKY;

Console.WriteLine("AGXIOME v1.0 — Agentic Generative C# X-Linked Integrated Orchestration Memory Engine");
Console.WriteLine("Built for .NET 10. Driven by LLMWIKY. Powered by PostgreSQL.\n");

// Example: create a minimal agent
// var agent = AgentFactory.Create("demo-agent", cfg =>
// {
//     cfg.ReasoningEngine = new LLMWIKYConfig
//     {
//         Provider = "OpenAI",
//         Model = "gpt-4o-mini",
//         ApiKey = Environment.GetEnvironmentVariable("LLMWIKY_API_KEY")
//     };
// });

Console.WriteLine("Ready. Wire up your LLMWIKY config and call agent.RunAsync().");
Console.WriteLine("See PLAN/ directory for full implementation details.");
