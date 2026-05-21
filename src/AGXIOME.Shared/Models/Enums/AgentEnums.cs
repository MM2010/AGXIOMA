namespace AGXIOME.Shared.Models;

/// <summary>Reasoning strategy for the agent: none, dedicated model, chain-of-thought agent, or tool-based.</summary>
public enum ReasoningMode { None, ReasoningModel, ReasoningAgent, ReasoningTools }

/// <summary>Determines how the agent's max-iteration limit is enforced.</summary>
public enum MaxIterationsMode { Fixed, Auto, Infinite }

/// <summary>Media type for multimodal content attached to agent messages.</summary>
public enum MediaType { Image, Audio, Video, Document }

/// <summary>How the media content is supplied: inline base64, remote URL, or local file path.</summary>
public enum MediaSource { Base64, Url, FilePath }

/// <summary>Transport protocol used for MCP (Model Context Protocol) server communication.</summary>
public enum MCPTransport { Stdio, SSE, StreamableHttp }
