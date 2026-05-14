namespace AGXIOME.Shared.Models;

public enum GuardrailType { Input, Output }
public enum GuardrailSeverity { Log, Flag, Block }
public enum HookPoint
{
    PreRun,
    PostRun,
    PreTool,
    PostTool,
    StreamChunk,
    BackgroundPost
}
public enum LearningMode { Always, Agentic, Propose }
public enum LearningSessionMode { Summary, Planning }
public enum EntityMemoryType { Fact, Event, Relationship }
public enum HITLRequirementType { Confirmation, UserInput, ExternalExecution, Audit }
public enum HITLStatus { Pending, Approved, Rejected, Expired }
public enum ApprovalType { Standard, Audit }
public enum ApprovalStatus { Pending, Approved, Rejected, Expired }
public enum ScheduleStatus { Active, Paused, Completed, Failed }
public enum ScheduleRunStatus { Running, Completed, Failed, Cancelled }
public enum RAGMode { Agentic, Traditional }
public enum SearchType { Vector, Keyword, Hybrid }
public enum DocumentType { PlainText, Markdown, Code, Pdf, Html, Csv }
public enum ReasoningMode { None, ReasoningModel, ReasoningAgent, ReasoningTools }
public enum MaxIterationsMode { Fixed, Auto, Infinite }
public enum MemoryBackend { InMemory, Postgres, MarkdownFiles, Hybrid }
public enum MediaType { Image, Audio, Video, Document }
public enum MediaSource { Base64, Url, FilePath }
public enum MCPTransport { Stdio, SSE, StreamableHttp }
public enum RunEvent { RunStart, RunContent, ToolCallStart, ToolCallEnd, RunPaused, RunCompleted, RunError }
public enum StepType { Agent, Function, Condition, Parallel, Loop, Router, Human, Delay }
public enum StepStatus { Pending, Running, Completed, Skipped, Failed }
public enum MergeStrategy { Concat, Vote, Weighted, SupervisorMerge }
public enum CompressionLevel { None, Gzip, Brotli }
public enum CheckTrigger { InputNotAllowed, OutputNotAllowed }
