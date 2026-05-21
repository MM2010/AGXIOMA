namespace AGXIOME.Shared.Models;

/// <summary>Type of human-in-the-loop requirement that pauses agent execution.</summary>
public enum HITLRequirementType { Confirmation, UserInput, ExternalExecution, Audit }

/// <summary>Lifecycle status of a HITL pause request.</summary>
public enum HITLStatus { Pending, Approved, Rejected, Expired }

/// <summary>Approval workflow type: standard single-approver or full audit trail.</summary>
public enum ApprovalType { Standard, Audit }

/// <summary>Lifecycle status of an approval request.</summary>
public enum ApprovalStatus { Pending, Approved, Rejected, Expired }
