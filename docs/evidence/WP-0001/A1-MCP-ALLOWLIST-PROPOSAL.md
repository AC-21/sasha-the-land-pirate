# WP-0001 A1 MCP allowlist proposal

Status: **A0 / proposed / unratified**

This document grants no authority, activates no packet, and authorizes no
Unity, Hub, Editor, relay, Codex, or MCP invocation. It records a least-
privilege proposal derived from static source inspection only. WP-0001 remains
`accepted` and blocked.

## Proposed creator decision

For the current schema-compatible activation boundary, expose exactly one
client-visible tool:

```json
["Unity_GetSha"]
```

Canonical JSON bytes:

```text
["Unity_GetSha"]
```

SHA-256:

```text
7edb30d30637816144223148ea4b9215edf3e77133f51a4a8956d9f69c4ed6e7
```

This is an **observation-only exposure**, not an invocation plan:

- the disconnected/revoked preflight invokes zero tools;
- the live activation session invokes zero tools;
- implementation phases 1 through 4 invoke zero Unity tools;
- any later invocation requires the still-valid live A1 session, a prompt-
  approved exact call, and no route, process, target, package, inventory,
  config, or allowlist drift;
- any allowlist expansion closes A1 and requires a separately protected
  boundary revision plus a fresh creator activation receipt.

The creator has not selected or bound this list. Until that happens, the
existing unresolved state remains authoritative.

## Why this one tool

`Unity_GetSha` is a native MCP tool registered but disabled by default. It
requires one caller-supplied C# script URI, rejects missing or invalid input,
requires an `Assets/` path, delegates only the fixed `get_sha` action, and
returns only SHA-256 and byte length. It contains no explicit refresh, import,
write, package operation, play-mode transition, code execution, content return,
or external-network call.

The schema-required protected seed, once created, permits no C# script, so an
early accidental call will fail closed. That is desirable: activation identity
will be proven by the required exact project path, socket, Editor PID, process
identities, config, package, and hash-bound route evidence. The schema-required
non-empty entry should be the least capable dormant sentinel, not a second
identity channel.

The path defense is not independently sufficient. `ManageScript` checks that
the normalized directory is under `Application.dataPath` and rejects detected
symlink ancestors, but the guard is best-effort, proceeds after metadata
errors, and does not inspect the final file as a symlink. The OS quarantine and
no-symlink-escape policy remain authoritative. Even if that defense failed,
this tool returns only a digest and length rather than file content.

The implementation also uses unbounded `File.ReadAllText` before hashing. An
oversized in-scope script can therefore create memory or availability pressure.
Zero invocation during activation, bounded candidate inputs, process resource
limits, and prompt approval remain required.

## Rejected one-tool alternatives

### `Unity_GetProjectData`

This is source-read-only and fixed to `Application.dataPath`, but it succeeds
without parameters, traverses the Assets database, and can return up to 5,000
items and 1,000,000 characters. Its declared read-permission check is
ineffective over direct MCP because the external AgentTool wrapper supplies
`AllowAllToolPermissions`. Its exposure also grows automatically as
implementation populates Assets. None of that is needed for a zero-call
activation sentinel.

### `Unity_GetUserGuidelines`

The surface appears parameterless and read-only, but resolving its configured
path lazily creates and writes
`ProjectSettings/Packages/com.unity.ai.assistant/Settings.json` when that file
is absent. Avoiding the write would require pre-seeding and binding the file.
Meaningful output would additionally require a GUID-targeted TextAsset, which
conflicts with the required protected empty-Assets seed. The returned custom
instructions can also contain up to 16,384 characters of candidate-controlled
prompt text.

### `Unity_ReadConsole`

This is not read-only at capability level. The same tool accepts
`Action=Clear`, so a client allowlist cannot permit reads while denying console
mutation.

### `Unity_GetConsoleLogs`

The implementation calls `AssetDatabase.Refresh()` before reading. That can
import externally written files, trigger compilation, and cause domain reload.

### Resource tools

`Unity_ListResources`, `Unity_ReadResource`, and `Unity_FindInFile` accept
caller-selected project roots or file URIs and can return project content.
They are broader than a single-script digest.

### Package, profiler, capture, and editor tools

- `Unity_PackageManager_GetData` may query the registry and use network/cache.
- The 14 profiler tools dereference a conversation context that direct MCP
  creates as `null`; static inspection indicates they may be unusable through
  this route.
- camera and Scene View tools create temporary Editor objects and perform
  screen capture.
- editor, scene, object, asset, script, shader, and menu tools combine reads
  with broad mutation or generic execution authority.

## Stock defaults are unsafe

The installed package exposes 54 tools: 20 native MCP tools and 34 Assistant
AgentTools. Seven are enabled by default:

```text
Unity_AssetGeneration_GenerateAsset
Unity_AssetGeneration_GetModels
Unity_Camera_Capture
Unity_GetConsoleLogs
Unity_RunCommand
Unity_SceneView_Capture2DScene
Unity_SceneView_CaptureMultiAngleSceneView
```

`Unity_RunCommand` compiles and executes arbitrary C# and is already
categorically forbidden by the WP-0001 boundary. Therefore the creator must
disable every stock default and enable only the exact reviewed candidate before
the first connection. The client-side `enabled_tools` list must match the
sanitized server inventory exactly.

Assistant Preferences are not an authorization boundary for direct MCP.
Assistant AgentTools are wrapped with
`ToolExecutionContextFactory.CreateForExternalCall`, which supplies
`AllowAllToolPermissions` and no conversation context. Native MCP tools do not
use that Assistant permission layer. Separately, Unity filters the advertised
tool list by UI settings but executes any registered named handler without
rechecking enabled state. Client allowlisting and the OS quarantine therefore
remain mandatory.

## Phase capability plan

| Phase | Unity MCP need | Proposed rule |
| --- | --- | --- |
| 0 — activation | none | Advertise the exact one-tool list; invoke zero tools. |
| 1 — static skeleton | none | Repository and locked dependency work only. |
| 2 — schema validator | none | Python/.NET validation and fixtures only. |
| 3 — deterministic core | none | Pure C# build and test path only. |
| 4 — save contracts | none | Pure C# and filesystem-fault tests only. |
| 5 — Unity adapter/native smoke | no safe stock primitive | Write reviewed source normally; creator runs the exact native wrapper. |
| 6 — renderer/calibration | no general primitive | Repository/Blender pipeline plus creator-run capture harness. |
| 7 — native acceptance | none for agent authority | Creator-operated wrapper, player launch, soak, and OS evidence. |
| 8 — handoff | none | Evidence, manifests, and bounded candidate handoff. |

Assistant 2.14 has no narrow built-in compile/test/build tool. The apparent
substitutes are overbroad:

- `Unity_ManageMenuItem` can execute nearly any Editor menu item; only
  `File/Quit` is blacklisted.
- `Unity_ManageEditor` combines state queries with play control and settings
  mutation.
- `Unity_RunCommand` is arbitrary C# execution and forbidden.

The recommended current path is therefore observation-only MCP plus a
creator-operated, content-addressed native wrapper.

If agent-driven native gates later become necessary, the safer successor is a
dedicated tool such as `Unity_WP0001_RunGate` with enumerated actions, fixed
inputs, fixed output paths, no arbitrary code/menu/path parameters, and
machine-readable results. The current protected seed forbids that code, and
adding it after activation changes the tool inventory. It requires a protected
seed/boundary revision and a fresh activation, not an allowlist edit in place.

## Exact inspected inventory

The following are client-visible sanitized names for the installed snapshot.
An asterisk marks the seven stock defaults.

```text
Unity_ApplyTextEdits
Unity_AssetGeneration_ConvertSpri_dca62520
Unity_AssetGeneration_ConvertToMaterial
Unity_AssetGeneration_ConvertToTe_debf7698
Unity_AssetGeneration_CreateAnima_40e1a9ab
Unity_AssetGeneration_EditAnimati_47017090
Unity_AssetGeneration_GenerateAsset *
Unity_AssetGeneration_GetComposit_832d2c69
Unity_AssetGeneration_GetModels *
Unity_AssetGeneration_ManageInterrupted
Unity_AudioClip_Edit
Unity_Camera_Capture *
Unity_CreateScript
Unity_DeleteScript
Unity_FindInFile
Unity_FindProjectAssets
Unity_GetConsoleLogs *
Unity_GetProjectData
Unity_GetSha
Unity_GetUserGuidelines
Unity_Grep
Unity_ImportExternalModel
Unity_ListResources
Unity_ManageAsset
Unity_ManageEditor
Unity_ManageGameObject
Unity_ManageMenuItem
Unity_ManageScene
Unity_ManageScript
Unity_ManageScript_capabilities
Unity_ManageShader
Unity_PackageManager_ExecuteAction
Unity_PackageManager_GetData
Unity_Profiler_GetBottomUpSampleT_55cc1e4e
Unity_Profiler_GetCounterSummary
Unity_Profiler_GetCounterTable
Unity_Profiler_GetFrameGcAllocati_a7eb5b61
Unity_Profiler_GetFrameRangeGcAll_90f409da
Unity_Profiler_GetFrameRangeTopTimeSummary
Unity_Profiler_GetFrameSelfTimeSa_e44ee448
Unity_Profiler_GetFrameTopTimeSam_ccc85b2d
Unity_Profiler_GetOverallGcAlloca_ac50c101
Unity_Profiler_GetRelatedSamplesT_a6086ba0
Unity_Profiler_GetSampleGcAllocat_4a279ae5
Unity_Profiler_GetSampleGcAllocat_89f626bb
Unity_Profiler_GetSampleTimeSumma_a680062a
Unity_Profiler_GetSampleTimeSummary
Unity_ReadConsole
Unity_ReadResource
Unity_RunCommand *
Unity_SceneView_Capture2DScene *
Unity_SceneView_CaptureMultiAngleSceneView *
Unity_ScriptApplyEdits
Unity_ValidateScript
```

The package sanitizes names by replacing `.` with `_`. Names longer than 42
characters are truncated to 33 characters and suffixed with `_` plus an
eight-hex-digit stable hash. The long names above are therefore exact; they
must not be reconstructed by simple dot replacement alone.

## Inspected snapshot

Package root:

`/Users/sasha/Sashas/Library/PackageCache/com.unity.ai.assistant@3dcd7d7fc635`

Package:

- name: `com.unity.ai.assistant`
- version: `2.14.0-pre.1`
- `package.json` SHA-256:
  `16e67e56d936d7812a10a067c730ef93e2fa0b809c8016ff1f35b0aaddcc2de9`

Key source identities:

| Artifact | SHA-256 |
| --- | --- |
| `McpToolRegistry.cs` | `85e496400146dfba6b51481b69aee706707777d69682c9d6202bb2c2ae5673ec` |
| `AgentToolMcpAdapter.cs` | `3a062b786142495dfee62b084b0e122e9f1dcc1be8660605706ea8f5f9922824` |
| `ToolExecutionContextFactory.cs` | `eff8aa4cfa8851580367477571d248fbeefb9acd479ee8b909c9b46fd15071f9` |
| `GetSHA.cs` | `67dddc025c678d17f5014407b39c6095dac9af22200e54cc0c0a07b496835b84` |
| `ManageScript.cs` | `8f2f53f4eae997eda5aae4b48ad9dd4f1b63e6e5500510c5507c36c8dce057f0` |
| `ScriptRefreshHelpers.cs` | `36b458fa120aed380ecbf4628c0dc69ca6dc017b4737047004e789b1658a411e` |
| `ProjectTools.cs` | `9327e63f8e501e7c88ed47473791ec122fa32ed5cab152255bf99a2455456057` |
| `UserGuidelineTools.cs` | `35a7bda04ec7125b66e5b92533ff1962405ca10bb5e7b698b57a6a71d39cefd0` |
| `AssistantProjectPreferences.cs` | `f2a19e96ccea5983dc847e7e588b4030df688a506c24cdf93757d898a80cbc39` |
| `ReadConsole.cs` | `83c8ca59bd19dfba46cf4afd2da7d43684e7c10db5bc4011b82603944a1f17d9` |
| `GetConsoleLogsTool.cs` | `f932696e4a3098c1530100af73c336bbb2e2bf26f003dcb0794670583e7aeb2d` |
| `RunCommand.cs` | `359056413fc062c900fb0402ec99bd08b04768023fc04ec624a9f1d4d05ec732` |

Relevant source locations in that snapshot:

- sanitization and 42-character limit:
  `McpToolRegistry.cs:57-85`;
- execution without enabled-state recheck:
  `McpToolRegistry.cs:215-244`;
- advertised-list filtering:
  `McpToolRegistry.cs:321-337`;
- AgentTool registration:
  `AgentToolMcpAdapter.cs:44-95`;
- external-call permissive context:
  `AgentToolMcpAdapter.cs:223-231` and
  `ToolExecutionContextFactory.cs:13-65`;
- proposed tool behavior:
  `GetSHA.cs:53-123` and `ManageScript.cs:313-329,407-428`;
- URI decoding and normalization:
  `ScriptRefreshHelpers.cs:82-170`;
- proposed tool path limitation:
  `ManageScript.cs:83-147`;
- rejected broad hierarchy behavior:
  `ProjectTools.cs:27-48`;
- hidden guidelines settings write:
  `AssistantProjectPreferences.cs:44-82`;
- console clear capability:
  `ReadConsole.cs:209-249`;
- console refresh side effect:
  `GetConsoleLogsTool.cs:48-67`;
- default arbitrary C# execution:
  `RunCommand.cs:102-126`.

No Unity, Hub, relay, MCP, or implementation process was invoked for this
inspection.

## Creator decisions required

To make this proposal binding, the creator and protected control plane must:

1. select the exact list or explicitly reject it;
2. bind the canonical list bytes and SHA-256 in the boundary and activation
   receipt using `AUTHORIZE-WP0001-MCP-ALLOWLIST`;
3. verify the live server advertises exactly that list after every stock
   default is disabled;
4. retain zero tool calls throughout preflight and activation;
5. choose whether phase-5 native work remains creator-operated or whether a
   separately protected narrow-tool revision is worth the added boundary.

The stricter long-term option is a protected schema revision that permits a
genuinely empty activation allowlist until a phase-specific tool exists. The
current schema requires a non-empty list, so that option cannot be adopted by
local documentation or an unprotected manifest edit.

## Recommendation

Ratify the one-tool observation-only list for the current boundary, keep all
Unity calls at zero through phase 4, and use creator-operated native wrappers
for phases 5 through 7. Do not expand the list to compensate for missing
build/test primitives.
