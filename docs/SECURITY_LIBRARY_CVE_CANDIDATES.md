# AGXIOME library residual risk and CVE-candidate assessment

## Executive summary

The AGXIOME library currently shows **no known vulnerable NuGet dependencies** from the package audit performed during this assessment, but it still contains several **high-risk first-party security flaws** that are stronger than generic hardening concerns and should be treated as **candidate CVEs** until remediated.

The highest-confidence issues are:

| Severity | Candidate issue | Affected area | Status |
| --- | --- | --- | --- |
| Critical | Syntax-only dynamic code validation can be bypassed, leading to in-process arbitrary host access/RCE | `AGXIOME.DynamicRuntime`, `apps/agxioma-dynamic-skill-lab` | Reproduced |
| High | Path traversal via untrusted `userId` / `agentId` enables writes outside the intended data root | `AGXIOME.DynamicRuntime`, skills persistence | Reproduced |
| High | LLM-controlled artifact paths can overwrite files outside the generation workspace | `AGXIOME.Core.Generator` | Confirmed by code path |
| High | Missing object-level authorization lets authenticated callers operate on arbitrary `userId` resources | `AGXIOME.Runtime` controllers | Confirmed by code path |

These issues are relevant at application level because the affected components sit on trust boundaries: dynamic source generation/compilation, filesystem persistence, and multi-user runtime APIs.

## Scope and method

This assessment focused on **library-level residual risk**, not only on sample applications. The review combined:

1. Dependency/SCA review on the .NET dependency graph.
2. Container/configuration review with Trivy.
3. Manual hotspot review of dynamic runtime, generator, runtime controllers, toolkits, and skills persistence.
4. Targeted offensive validation with a temporary .NET harness that invoked the relevant validators directly.

Tooling notes:

- `dotnet list package --vulnerable --include-transitive`: **no vulnerable packages found**
- Trivy filesystem/misconfiguration scan: **no package CVEs**, but Docker misconfig findings such as root user and missing healthcheck
- Semgrep attempt was inconclusive in this environment because rule download from `semgrep.dev` failed due SSL/network issues

## Findings

### 1. Critical: dynamic code policy bypass leads to arbitrary host access

**Affected files**

- `src/AGXIOME.DynamicRuntime/Compilation/AgentCodeValidator.cs`
- `src/AGXIOME.DynamicRuntime/Compilation/AgentCompiler.cs`
- `src/AGXIOME.DynamicRuntime/Bridge/AgentServices.cs`
- `apps/agxioma-dynamic-skill-lab/Engine/DynamicToolSourcePolicy.cs`
- `apps/agxioma-dynamic-skill-lab/Engine/RoslynCompiler.cs`
- `apps/agxioma-dynamic-skill-lab/Engine/SandboxExecutor.cs`

**Why it is dangerous**

The validators are syntax-based and do not perform semantic symbol resolution. They try to block dangerous APIs by inspecting `using` directives, identifier names, and string forms of member access. This is not a security boundary.

In `AgentCodeValidator`, blocked member access is detected only when the expression text starts directly with a blocked prefix:

- `BlockedNamespacePrefixes` includes `System.IO`, `System.Reflection`, `System.Diagnostics.Process`, `System.Runtime.InteropServices`, `System.Security`, `System.Net.Sockets`, `Microsoft.Win32`
- the check is `expr.StartsWith(blocked, StringComparison.Ordinal)` on `MemberAccessExpressionSyntax`

That means alternate syntactic forms can evade detection.

In `DynamicToolSourcePolicy`, forbidden namespaces are checked only in `using` directives and forbidden types are checked only against `IdentifierNameSyntax`. A fully-qualified call can avoid both checks if the final type name is not in the denylist.

**Reproduction**

A temporary harness invoked the validators directly with the following patterns:

- `global::System.IO.File.ReadAllText("/etc/hosts")` against `AgentCodeValidator`
- `new global::System.Net.Http.HttpClient()` against `DynamicToolSourcePolicy`

Observed result:

- `AgentCodeValidator.Ok=True`
- `DynamicToolSourcePolicy.Errors=0`

So the payloads were accepted by the security validators even though they perform filesystem/network actions that the policy is clearly trying to forbid.

**Impact**

If untrusted or insufficiently trusted users can influence dynamic source, this becomes an in-process arbitrary code execution problem with access to:

- local filesystem
- outbound network
- shared process memory and services
- any credentials reachable by the host process or exposed through toolkit/runtime services

`AssemblyLoadContext` does not provide OS/process isolation, so this is not a sandbox.

**CVE-candidate rationale**

This is not merely “unsafe by design.” The code explicitly implements a security policy and presents a sandbox-like control model, but that policy is bypassable with straightforward syntax variation. That is a strong candidate for a vulnerability record if the feature is exposed in production scenarios.

## 2. High: path traversal via `userId` / `agentId`

**Affected files**

- `src/AGXIOME.DynamicRuntime/Generation/AgentCodeGenerator.cs`
- `src/AGXIOME.DynamicRuntime/Skills/SkillsShRegistry.cs`
- related registry/runtime flows that trust caller-provided identifiers

**Evidence**

`AgentCodeGenerator` builds paths from untrusted values:

- `Path.Combine(_basePath, "agents", request.UserId, "src")`
- `Path.Combine(srcPath, $"{agentId}.cs")`

`SkillsShRegistry` builds:

- `Path.Combine(_basePath, $"{userId}.json")`

There is no canonicalization plus root-prefix enforcement before write.

**Reproduction**

The temporary harness used:

- `userId = "../../../../outside"`
- `agentId = "../../../payload"`

Observed result:

- `srcPath=/outside/src`
- `srcFile=/payload.cs`
- `skillsFile=/outside.json`

So attacker-controlled identifiers can escape the intended base directory when later normalized by the OS path rules.

**Impact**

- arbitrary file creation/overwrite outside AGXIOME storage roots
- cross-tenant corruption
- persistence tampering
- possible overwrite of operational files if the process identity has sufficient permissions

**CVE-candidate rationale**

This is a classic CWE-22/CWE-73 style issue on a write path reachable from runtime/user-facing operations.

## 3. High: LLM-controlled file path overwrite in project generation

**Affected files**

- `src/AGXIOME.Core/Generator/AgenticCodeWriter.cs`
- `src/AGXIOME.Core/Generator/ProjectGenerator.cs`

**Evidence**

`AgenticCodeWriter.ExtractCodeAndPath` accepts a file path from the LLM response code fence metadata and returns it as `extractedPath`.

`ProjectGenerator` later writes the artifact with:

```csharp
var fullPath = Path.Combine(Path.GetFullPath(_outputPath), artifact.FilePath);
Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
await File.WriteAllTextAsync(fullPath, artifact.Code, ct);
```

There is no visible `GetFullPath` normalization of the final combined path followed by an “inside output root” check.

**Impact**

If the generation pipeline accepts a malicious or manipulated model response, it may write outside the intended workspace and overwrite arbitrary files reachable by the process identity.

**Assessment**

This issue is strongly supported by code inspection. It was not turned into a destructive live overwrite during this assessment, but the sink is direct enough that it should be treated as a real vulnerability until bounded-path validation is added.

## 4. High: missing object-level authorization on `userId`-scoped APIs

**Affected files**

- `src/AGXIOME.Runtime/Controllers/DynamicAgentController.cs`
- `src/AGXIOME.Runtime/Controllers/SkillsController.cs`
- relevant auth middleware paths in runtime

**Evidence**

The controllers trust caller-supplied `userId` parameters for operations such as:

- generate/list/remove/compile dynamic agents
- import/list/trust skills

Representative examples:

- `GET /api/agents/user/{userId}`
- `POST /api/skills/{skillId}/import/{userId}`
- `GET /api/skills/user/{userId}`
- `PATCH /api/skills/{skillId}/trust?userId=...`

The controller code does not bind `userId` to the authenticated principal before invoking backend services.

**Impact**

An authenticated user may be able to operate on another tenant/user's runtime state by swapping identifiers, leading to IDOR/BOLA-style access control failure.

**CVE-candidate rationale**

Where AGXIOME is deployed as a multi-user service, this is a high-risk authorization vulnerability rather than a generic design preference.

## Additional risk surfaces

These did not rise to the top four candidate CVEs, but they materially amplify impact:

- `AGXIOME.Toolkits/Development/DevelopmentToolkits.cs` exposes shell, Python, git, docker, and build wrappers
- `AGXIOME.Toolkits/CloudInfra/CloudInfraToolkits.cs` passes cloud credentials into subprocess environments
- `AgentServices` exposes shared runtime services, including HTTP/LLM access, to generated code

These surfaces are extremely sensitive if ever reachable from low-trust prompts, tool invocations, or dynamic-agent flows.

## Residual risk rating

**Current residual risk: High**

The absence of third-party dependency CVEs does not materially reduce the production risk from the first-party issues above. The library currently contains multiple trust-boundary weaknesses that can plausibly yield:

- arbitrary host access from dynamic code features
- filesystem escape and overwrite
- cross-user data access and state manipulation
- secret exfiltration through runtime/tooling/network access

## Recommended remediation order

1. **Stop treating syntax validation as sandboxing.** Move dynamic execution to a real isolation boundary: separate process/container/jail with strict syscall/network/filesystem restrictions.
2. **Add canonical path enforcement everywhere untrusted identifiers or LLM paths influence storage.** Normalize with `Path.GetFullPath`, then reject any path not rooted under the intended base directory.
3. **Bind `userId` to the authenticated principal** or enforce explicit tenant authorization checks in all user-scoped controllers/services.
4. **Restrict or disable dangerous toolkits by default** in any environment where prompts, agents, or users are not fully trusted.
5. **Add regression tests for the exact bypasses reproduced here**, especially:
   - `global::System.IO.*`
   - fully-qualified `System.Net.*`
   - `../` traversal in `userId`, `agentId`, and generated artifact paths

## Bottom line

AGXIOME does not currently show known package CVEs from the dependency audit, but it **does** show several credible first-party vulnerability candidates, including one **reproduced critical dynamic-execution bypass** and one **reproduced path traversal escape**. These should be fixed before considering the affected dynamic runtime and generation features production-ready in hostile or semi-trusted environments.
