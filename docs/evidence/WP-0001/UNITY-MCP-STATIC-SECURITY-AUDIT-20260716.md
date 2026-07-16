# Unity MCP static security audit — 2026-07-16

Packet: `WP-0001`

Route: `UNITY-MCP-EXTERNAL`

Authority: A0 observation only
Verdict: **stock Unity Assistant 2.14 direct MCP is not, by itself, an A1
quarantine boundary**

No Unity, Hub, relay, MCP, or implementation process was invoked for this
audit. Findings came from read-only inspection of the installed package,
existing secret-free files, and published Codex configuration documentation.

## Audited snapshot

Installed package root:

`/Users/sasha/Sashas/Library/PackageCache/com.unity.ai.assistant@3dcd7d7fc635`

Package identity:

- name: `com.unity.ai.assistant`
- version: `2.14.0-pre.1`
- `package.json` SHA-256:
  `16e67e56d936d7812a10a067c730ef93e2fa0b809c8016ff1f35b0aaddcc2de9`

The earlier readiness record binds the observed relay, Codex client, project,
connection file, package locks, host, and all three deviations. This audit does
not replace that record.

## Findings

### F-01 — Tool visibility is not execution authorization

Severity for A1: **blocking**

The settings UI and `get_available_tools` path filter which tools are
advertised. `MCPSettings.IsToolEnabled` drives the advertised list, but
`McpToolRegistry.ExecuteToolAsync` looks up the complete registry and executes
the named handler without checking enabled state.

Evidence:

- `Modules/Unity.AI.MCP.Editor/Settings/MCPSettings.cs:76-120`
- `Modules/Unity.AI.MCP.Editor/ToolRegistry/McpToolRegistry.cs:321-337`
- `Modules/Unity.AI.MCP.Editor/ToolRegistry/McpToolRegistry.cs:229-244`
- source SHA-256:
  - `MCPSettings.cs`
    `7fd7e9b7fbef66988459e5a535694b6077712b5f677f1f4f2d8d86efd6ada884`
  - `McpToolRegistry.cs`
    `85e496400146dfba6b51481b69aee706707777d69682c9d6202bb2c2ae5673ec`

Consequence:

- “7 of 54 enabled,” zero advertised tools, category filtering, and UI
  checkboxes are not Unity-side authorization boundaries.
- A client that sends a registered hidden tool name can still reach the
  handler.
- The default enabled set includes an arbitrary C# command tool, so defaults
  are not an acceptable least-privilege profile.

Required mitigation:

1. The repo-scoped Codex MCP entry must use an exact `enabled_tools` allowlist
   and prompt-based approval as defense in depth.
2. The OS quarantine must remain authoritative: unapproved tools must still be
   unable to reach protected files, credentials, ambient HOME, shared temp, or
   unrestricted network.
3. A creator-owned, content-addressed Unity bootstrap that unregisters
   non-allowlisted tools may add server-side defense, but it cannot be assumed
   safe until its load order and refresh behavior are proven on the exact
   package.

Codex officially documents `enabled_tools` as an MCP allowlist, `disabled_tools`
as a later deny list, and `env` as variables forwarded to a stdio server:
<https://developers.openai.com/codex/config-reference/>.

### F-02 — Pending and unknown connections are accept-by-default for tool calls

Severity for A1: **blocking**

The bridge rejects tool calls only when connection state is `Denied`. Its own
comments state that tool calls are allowed in every other state, including
`Unknown`, `Validating`, and `AwaitingApproval`. A new connection is placed in
`AwaitingApproval`, so the approval screen is not a complete execution gate.

Standalone MCP tool-approval requests also return approved when no valid
Gateway session token exists.

Evidence:

- `Modules/Unity.AI.MCP.Editor/Bridge.cs:1250-1264`
- `Modules/Unity.AI.MCP.Editor/Bridge.cs:1750-1784`
- `Modules/Unity.AI.MCP.Editor/Bridge.cs:1849-1859`
- `Bridge.cs` SHA-256:
  `6fc96d2dcccfb8efe3936d4e2e691914a2c0a0663503f8aa21473885652a2427`

Required mitigation:

- The first connection may occur only inside the dedicated ephemeral
  principal/network boundary, with no other same-user process and no reachable
  remote client.
- The exact client-side allowlist must already be active before connection.
- The clean setup cycle sends no model prompt and invokes zero Unity tools.
- Failure to prove those controls leaves A1 infeasible.

### F-03 — Approval identity is not an exact client-version hash pin

Severity for A1: **blocking**

For a valid signed executable, Unity keys identity by executable path plus
publisher so approval survives binary updates. It also falls back to matching
the server/client publisher pair and automatically accepts a previously
accepted match.

Evidence:

- `Modules/Unity.AI.MCP.Editor/Security/ExecutableIdentityComparer.cs:49-76`
- `Modules/Unity.AI.MCP.Editor/Connection/ConnectionStore.cs:178-209`
- `Modules/Unity.AI.MCP.Editor/Bridge.cs:1221-1247`
- source SHA-256:
  - `ExecutableIdentityComparer.cs`
    `0f8776d8fd8ca72fd1b6d4dc547cb0e978544511e3216550bb85749fef863f41`
  - `ConnectionStore.cs`
    `be197a17164517955b1bf1926a3a4d5096ac66c346b2e480a1e3ac296ba81546`

Required mitigation:

- Bind the exact Codex executable path, version, SHA-256, signing identity, and
  creator-observed connection record in activation evidence.
- Enforce the exact executable and ordered relay arguments outside Unity,
  including the project target and current Editor PID.
- Revoke and disconnect on any client, relay, package, target, or policy drift.
- Never treat Unity’s “previously approved” label as exact-version proof.

### F-04 — The persistent Assistant relay opens a separate client port

Severity for A1: **blocking until suppressed or network-denied**

The package automatically starts persistent relay mode. It allocates a
WebSocket port and an MCP-client REST port in `9001-9100`, then launches the
same relay binary with `--relay`, `--port`, and `--mcp-client-port`. The stock
launcher exposes no host/bind argument. The observed readiness snapshot found
the MCP-client listener on all interfaces.

This is separate from the external direct-MCP stdio process launched with
`--mcp`.

Evidence:

- `Editor/Assistant/Relay/RelayService.cs:67-97`
- `Editor/Assistant/Relay/RelayService.cs:105-134`
- `Editor/Assistant/Relay/RelayService.cs:312-326`
- `Editor/Assistant/Relay/RelayService.cs:574-609`
- `Editor/Assistant/Relay/RelayService.cs:1102-1135`
- `RelayService.cs` SHA-256:
  `529735e5241648679133c5bcc296d25455b519182e0946f529059a8a68ca11a6`

Required mitigation, in order of preference:

1. Prove a creator-owned, immutable bootstrap suppresses persistent relay
   auto-start before its deferred callback, without later lazy initialization.
2. Otherwise bind an OS network policy that denies non-loopback inbound access
   and prove it against the live listener inventory.
3. If neither can be proven on the exact tuple, do not activate A1.

Do not patch or replace Unity’s signed relay binary.

### F-05 — Stock Codex auto-configuration omits required targeting and policy

Severity for A1: **blocking**

Unity’s Codex integration writes only the relay command and `--mcp`. It does
not add the exact project path, Editor PID, private runtime environment, tool
allowlist, or approval policy.

Evidence:

- `Modules/Unity.AI.MCP.Editor/Settings/Integration/CodexIntegration.cs:100-135`
- `Documentation~/integration/unity-mcp-get-started.md:74-108`
- source SHA-256:
  - `CodexIntegration.cs`
    `8ac378857f0407f9d4165f729a560ff49c0865f04635c998c756b46bdbfa6fde`
  - `unity-mcp-get-started.md`
    `b38e949cb3652198e455c87a4dcd5411b7fc39b0e8a3b5840768b0018320f9dc`

Required mitigation:

- Do not use Unity’s one-click Codex configuration as activation evidence.
- Use the sole active runtime configuration at
  `<ephemeral-home>/.codex/config.toml`, while the protected project entry
  stays disabled, with:
  - the exact relay executable;
  - `--mcp`;
  - exact absolute `--project-path <candidate>/Game`;
  - current `--instance-id <editor-pid>`;
  - exact client allowlist;
  - prompt-based tool approval;
  - private `HOME`, `TMPDIR`, `TMP`, and `TEMP`;
  - `required = true`;
  - no fallback/global Unity MCP entry.

### F-06 — Mutable MCP state spans project, HOME, and hard-coded shared temp

Severity for A1: **blocking until physically isolated**

Observed package behavior:

- connection approvals persist per project in
  `Library/AI.MCP/connections-v2.asset`;
- relay and bridge-discovery state defaults under the user profile in
  `~/.unity/`;
- tool/settings overrides use global Unity Editor preferences;
- the Unity bridge socket is under `/tmp`;
- the socket is permission-restricted, but its shared-temp location is not
  moved by ordinary `TMPDIR` binding.

Evidence:

- `Modules/Unity.AI.MCP.Editor/Connection/ConnectionRegistry.cs:10-52`
- `Modules/Unity.AI.MCP.Editor/Settings/MCPConstants.cs:57-110`
- `Modules/Unity.AI.MCP.Editor/Settings/MCPSettingsManager.cs:29-50`
- source SHA-256:
  - `ConnectionRegistry.cs`
    `8351065d518d5361ad720040df99f87008b1d8b25c26d37de5c035b37f35ccc5`
  - `MCPConstants.cs`
    `41659294a8af8e6e2b39469cb941aefbaa70939d49948dbbcfd5b391b82f21da`

Required mitigation:

- Use a dedicated ephemeral OS user or equivalent native OS sandbox.
- Deny writes to the creator’s HOME and default-deny ambient shared temp,
  except the exact project-hash/PID socket path required by the Bridge.
- Ensure the private HOME contains only the selected bridge/relay state.
- Bind the exact `/tmp` socket path, ownership, mode, Editor PID, and project.
- Destroy project scratch, runtime HOME, private temp, connection history, and
  route state when the quarantine closes.

## Project-seed control gap

WP-0001 allows the creator to create the temporary Unity project and
Editor-generated package locks before A1, but the current boundary manifest
binds only the Git base commit. Protected main currently contains no `Game/`
project.

A dirty candidate with unbound creator-generated bytes is not a reproducible
starting state. Boundary schema v4 therefore accepts only a creator-created
protected setup commit before the final A1 base is frozen. It permits only
ProjectSettings, manifest/lock, and an empty Assets marker; rejects embedded
packages and local/Git/file sources; binds the complete Unity-registry package
graph; and requires committed `project-seed.json` evidence.

The physical protected seed does not yet exist. Until the creator creates and
imports it, activation remains blocked.

## A1 acceptance position

The direct-MCP route can remain the selected route, but only as one component
inside a stronger physical boundary.

A1 must remain blocked unless all of the following are simultaneously true:

- exact D-0047 toolchain and package tuple;
- eligible assigned Unity AI seat and same-organization project link;
- content-addressed creator project seed;
- dedicated ephemeral principal with denied ambient writes and credentials;
- persistent relay suppressed or OS-isolated from non-loopback access;
- exact relay/client/project/PID/hash binding;
- exact Codex allowlist and prompt approval policy;
- Unity package weaknesses explicitly recorded and mitigated;
- fresh first-connection cycle with zero model prompt and zero Unity tool calls;
- exact evidence and boundary manifests;
- separate sealed creator activation receipt.

If any item is missing, the correct result is still A0/blocked.

## Known limits

- This was static analysis, not exploit execution or dynamic penetration
  testing.
- The signed relay binary was not modified or invoked.
- The exact non-empty post-activation allowlist still requires creator
  selection. Its sanitized list and digest must be bound by
  `AUTHORIZE-WP0001-MCP-ALLOWLIST`; `Unity_RunCommand`,
  `Unity_PackageManager_ExecuteAction`, and `Unity_ImportExternalModel` are
  categorically excluded at A1.
- A Unity bootstrap that unregisters tools is only a proposed defense until its
  assembly access, initialization order, registry refresh, and package-drift
  behavior are proven.
- No vulnerability report was sent to Unity as part of this work.
