# A1 Boundary Manifest Authoring Worksheet

This worksheet is not authority. Before an A1 packet runs, a creator-controlled process writes `governance/a1-boundaries/<packet-id>.json` conforming to [`../schemas/a1-boundary-manifest.schema.json`](../schemas/a1-boundary-manifest.schema.json), hashes its raw bytes, stores that exact reference on the packet, and seals one `packet-activation` receipt over the same hash.

## Immutable identity

- Schema version: `3`
- Manifest ID:
- Packet ID:
- Packet contract SHA-256:
- Creator/gatekeeper attestor:
- Activation receipt ID:

## Repository and reservation

- Local Git repository root:
- Exact base commit:
- Lease ID and fencing token:
- Expiry:
- Exact reserved paths:
- Exact reserved state domains:

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
- Ambient creator-home writes denied: `true`
- Ambient shared-temp writes denied: `true`
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

Unity, relay, licensing, package-cache, log, and MCP connection state outside the repository must resolve only inside the disposable runtime HOME/private temp. The creator's ambient HOME and shared temp namespaces are denied, and the exact sandbox/network policies are hash-bound. These runtime roots are boundary infrastructure, never reservation paths or creator-import candidates.

## Creator import boundary

- Mode: `creator-operated-import-or-reject`
- Agent merge/release/evidence-acceptance capabilities: all `false`
- Creator review required: `true`

## Local observation exceptions

List baseline evidence IDs that cannot be content-addressed before execution. Each must use a concrete `local-observation://` URI; `pending://` is never valid for an active packet. An empty list is preferred.
