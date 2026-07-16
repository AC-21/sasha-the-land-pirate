# A1 Boundary Manifest Authoring Worksheet

This worksheet is not authority. Before an A1 packet runs, a creator-controlled process writes `governance/a1-boundaries/<packet-id>.json` conforming to [`../schemas/a1-boundary-manifest.schema.json`](../schemas/a1-boundary-manifest.schema.json), hashes its raw bytes, stores that exact reference on the packet, and seals one `packet-activation` receipt over the same hash.

## Immutable identity

- Schema version: `4`
- Manifest ID:
- Packet ID:
- Packet contract SHA-256:
- Creator/gatekeeper attestor:
- Activation receipt ID:

## Repository and reservation

- Local Git repository root:
- Exact absolute detached-clone root:
- Exact base commit:
- `.git` is an independent directory and common directory:
- Detached HEAD, zero remotes, and no alternates:
- Lease ID and fencing token:
- Expiry:
- Exact reserved paths:
- Exact reserved state domains:

For WP-0001, the creator must first commit the temporary Unity project seed to
the protected base. Bind the raw SHA-256 of `git ls-tree -r -z <base> -- Game`,
the committed `project-seed.json` evidence, and the attestation that the seed
contains setup only—not packet implementation. A dirty candidate-only project
seed is not executable authority. The seed permits only ProjectSettings, the
manifest/lock, and an empty Assets marker; embedded packages and local/Git/file
dependencies are forbidden, and the complete `com.unity.*` registry/builtin
graph is bound.

## Approved execution tuple

- Tool name, exact version, source, installer/archive SHA-256 when available:
- OS and exact version:
- Architecture and non-secret hardware ID/digest:
- Sandbox profile ID/path and raw SHA-256:
- Network policy ID/path and raw SHA-256:

## Disposable runtime boundary

- Isolation mode: `dedicated-ephemeral-os-user` or `equivalent-os-sandbox`
- Runtime instance ID, principal ID, and exact UID:
- Exact absolute ephemeral `HOME` root:
- Exact absolute private temp root:
- `HOME`, `TMPDIR`, `TMP`, and `TEMP` bindings (must equal those roots):
- Exact ambient creator-home root:
- Exact ambient shared-temp roots:
- Exact denied ambient write roots (creator home followed by every shared-temp root):
- Exact shared-temp write exceptions (empty except packet-bound paths):
- Ambient creator-home writes denied: `true`
- Ambient shared-temp writes default-denied outside exact exceptions: `true`
- Symlink escape forbidden and all bound roots verified symlink-free: `true`
- Runtime roots importable as packet output: `false`
- Destroy the runtime HOME/private temp when quarantine closes: `true`

## Foundation binding

- `00-GAME-CONSTITUTION.md` SHA-256:
- `ledger/decisions.jsonl` SHA-256:
- Last sealed creator receipt ID:

## Protection and credentials

- Protected paths mounted read-only or absent:
- Writable paths (must exactly equal the reservation):
- Ephemeral scratch paths (non-output; must be exact, disjoint from protected/reserved paths, and repository-ignored):
- Destroy all ephemeral scratch when the quarantine closes: `true`
- Approved credential IDs:
- Denied merge, release, protected-main, governance, and receipt-write capabilities:

For WP-0001, the exact permitted scratch roots are `Game/Library/`, `Game/Temp/`, `Game/Logs/`, `Game/Obj/`, `Game/UserSettings/`, `Game/MemoryCaptures/`, and `Game/Recordings/`. `Game/Builds/` is not scratch authority: build evidence must be written directly to the packet-declared `BuildArtifacts/WP-0001/` paths. Nothing under a scratch root may be imported; any retained evidence must first be copied into a declared output path and recorded in the packet evidence manifest.

Unity, relay, licensing, package-cache, log, and MCP connection state outside the repository must resolve only inside the disposable runtime HOME/private temp. The creator's ambient HOME and shared temp namespaces are default-denied, except that WP-0001 permits exactly its project-hash/PID Bridge socket under resolved `/private/tmp/`. The exact sandbox/network policies are hash-bound. These runtime roots are boundary infrastructure, never reservation paths or creator-import candidates.

Bind the client environment separately: `CODEX_HOME`, XDG config/cache/data
roots, `GIT_CONFIG_NOSYSTEM=1`, an isolated `GIT_CONFIG_GLOBAL`,
`GIT_TERMINAL_PROMPT=0`, and the exact absent credential-variable list. The
client, preflight, and activation session carry the canonical environment
SHA-256.

## WP-0001 direct MCP route

Bind all of the following in `unity_mcp_route`:

- exact Codex executable path, version, SHA-256, publisher metadata, strict
  component-signature verification plus Identifier, TeamIdentifier, CDHash,
  designated-requirement hash and authority-list hash, PID, process start,
  boot-session-bound birth ID, UID, and candidate working directory;
- exact Unity-installed relay path, package-copy match, version, PID, process
  start, birth ID, strict component-signature tuple, parent Codex PID, UID, and
  arguments;
- exact Bridge Editor PID/start/birth ID, candidate `Game` target,
  strict component-signature tuple, connection-file hash, discovery label
  `named_pipe`, physical Unix socket, project-hash/PID endpoint, owner UID,
  mode `0600`, and single shared-temp exception;
- creator receipt claim `AUTHORIZE-WP0001-CODE-IDENTITIES` over all three
  strict-signature tuples;
- eligible assigned Unity AI seat plus same-organization project linkage,
  without copying license secrets;
- exact temporary company, product, bundle, and dev/test profiles;
- separate runtime Codex config under the disposable HOME, while the protected
  repo `.codex/config.toml` stays disabled;
- server name `unity_mcp_a1_wp0001`, required startup, `on-request` approval,
  prompt-based MCP tool approval, exact non-empty sanitized `enabled_tools`,
  matching client-visible inventory, canonical-list SHA-256, and creator claim
  `AUTHORIZE-WP0001-MCP-ALLOWLIST`;
- canonical effective-server-inventory SHA-256 bound by the client, preflight,
  and activation session, with exactly one active runtime server and no
  ancestor/project extras;
- no inherited approval, no publisher fallback, batch auto-approval off, and
  Gateway disabled;
- disconnected/revoked preflight handshake with zero model prompts/tools,
  followed by a distinct creator-approved live activation session with exact
  session/connection hashes and zero model prompts/tools;
- explicit acknowledgement that stock Unity can execute hidden tools by name
  and can execute while approval is pending;
- either zero persistent-relay processes/listeners, or a hash-bound OS network
  denial around them; both modes require a boot-bound listener census, failed
  non-loopback and unapproved-egress probes, and successful loopback and
  approved-egress controls.

The activation receipt binds every referenced evidence hash and must be issued
within five minutes of the live-session capture. PID/birth evidence expires on
any disconnect, Editor/Codex/relay restart, target drift, config drift, or
allowlist drift.

The live route capture binds a canonical route-contract projection that omits
only evidence-reference fields pointing back to the capture chain. It must not
hash the final manifest bytes and thereby create a circular evidence fixed
point.

## WP-0001 raw capture collectors

- Exact protocol collector:
  `docs/foundation-v0.1/tools/capture_wp0001_protocol.py`
- Exact network collector:
  `docs/foundation-v0.1/tools/capture_wp0001_network.py`
- Exact policy-attachment collector:
  `docs/foundation-v0.1/tools/capture_wp0001_policy_attachment.py`
- Raw SHA-256 of each collector source:
- Creator receipt claim: `AUTHORIZE-WP0001-RAW-COLLECTORS`

Every raw capture must embed its exact collector path, hash, and fixed
`/usr/bin/python3 <collector> <capture-kind>` invocation. The network capture
binds an external observer, the canonical Bridge control, approved Unity
Registry egress, and denied unapproved egress. The policy-attachment capture
binds the exact sandbox/network hashes to all three process birth identities.
Missing or inert collectors, a hash/command mismatch, or an absent authority
claim blocks A1.

## Creator import boundary

- Mode: `creator-operated-import-or-reject`
- Agent merge/release/evidence-acceptance capabilities: all `false`
- Creator review required: `true`

## Local observation exceptions

List baseline evidence IDs that cannot be content-addressed before execution. Each must use a concrete `local-observation://` URI; `pending://` is never valid for an active packet. An empty list is preferred.
