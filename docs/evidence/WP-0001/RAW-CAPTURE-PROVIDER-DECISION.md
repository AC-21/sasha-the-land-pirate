# WP-0001 raw-capture provider decision

Status: **A0 / proposed / unresolved**

This document grants no authority, activates no packet, and authorizes no
Unity, Hub, Editor, relay, Codex, or MCP invocation. It records a provider gap
found through static inspection only. WP-0001 remains `accepted` and blocked.

## Static observation

The installed relay help text, inspected only as static embedded source,
exposes `--debug`, `--log`, and `--log-dir`. It exposes no documented raw-audit
option that promises the exact inbound and outbound MCP frames, byte order, or
request IDs required by the activation evidence contract. No relay executable
or help command was run to make this observation.

The inspected package was Assistant `2.14.0-pre.1` at
`/Users/sasha/Sashas/Library/PackageCache/com.unity.ai.assistant@3dcd7d7fc635`.
The relay binary is 65,843,264 bytes. Read-only byte searches found the embedded
help heading at offset `62738432`, `--debug` at `62739033`, `--log` at
`62739088`, `--log-dir` at `62739168`, `--project-path` at `62739317`, and
`--instance-id` at `62739400`. The complete 1,190-byte embedded help block is
the half-open byte range `[62738432, 62739622)` and has SHA-256
`be400c61137fd61799be3e9e5fa9bce9f1f5f97d733fe1568733175ae8259ff3`.
The observation is reproducible without running the binary:

```bash
rg -aob \
  'Unity AI Relay Server|--debug|--log,|--log-dir|--project-path|--instance-id' \
  /Users/sasha/.unity/relay/relay_mac_arm64.app/Contents/MacOS/relay_mac_arm64
dd if=/Users/sasha/.unity/relay/relay_mac_arm64.app/Contents/MacOS/relay_mac_arm64 \
  bs=1 skip=62738432 count=1190 2>/dev/null | shasum -a 256
dd if=/Users/sasha/.unity/relay/relay_mac_arm64.app/Contents/MacOS/relay_mac_arm64 \
  bs=1 skip=62738432 count=1190 2>/dev/null | strings -a
```

`Bridge.cs:792-802` logs a handshake summary and tool count,
`Bridge.cs:895-902` reads and deserializes a command without retaining its raw
bytes, `Bridge.cs:1221-1376` logs approval transitions, and
`Bridge.cs:1720-1747` logs tool-inventory summaries. `MessageProtocol.cs:90-137`
defines the newline-delimited transport but no retained raw-frame audit. Those
diagnostics are useful, but they do not preserve the exact frames or
request/response IDs needed to prove the required `initialize` and `tools/list`
exchange.

Observed static identities:

| Artifact | SHA-256 |
| --- | --- |
| installed relay | `e52d9dc5380297456dc9ae168bdc981e7344651e653b73424dd4bc88df26eaf1` |
| embedded `relay.json` | `2fb983cfc88b63722c65f6b16ba0ab1586d839b0d3f3d6485010fd0048d311d2` |
| `Bridge.cs` | `6fc96d2dcccfb8efe3936d4e2e691914a2c0a0663503f8aa21473885652a2427` |
| `MessageProtocol.cs` | `6238aa72dadf753f8bbda9b23debd25b791d9297ba4ccdbb40a4c08aea59560b` |

These hashes identify the inspected bytes; they do not convert diagnostic logs
into authoritative raw evidence.

## Three independent provider gaps

### 1. Protocol provider

Required evidence is an ordered, lossless capture of the exact MCP
`initialize` request/response, `initialized` notification, and `tools/list`
request/response, including original request IDs. The current relay and Bridge
diagnostics do not satisfy that requirement.

Reversible provider options:

1. a vendor-supported relay/Bridge audit output that explicitly guarantees
   lossless raw frames and IDs;
2. a creator-ratified, content-addressed stdio/socket observation boundary
   inserted into the direct route, with the changed route identity included in
   the activation contract;
3. an independent OS tracing facility proven to recover those same bytes
   without changing or controlling the target processes.

### 2. Network provider

Required evidence is an independent listener census plus actual probes for the
canonical Bridge Unix socket, non-loopback denial, approved egress, and
unapproved egress. Application-authored summaries cannot prove either policy
enforcement or failed traffic.

Reversible provider options:

1. creator-operated OS-native socket, process, and packet observation with
   fixed commands and raw outputs;
2. a hash-bound firewall/sandbox provider that exports authoritative listener,
   flow, and denial records;
3. an isolated host or VM boundary whose external observer records both
   permitted and denied probes.

### 3. Policy-attachment provider

Required evidence must prove that the exact sandbox and network policy hashes
were attached to the exact client, relay, and Editor process births. A policy
file on disk and a process's own claim are insufficient.

Reversible provider options:

1. an OS enforcement facility with externally readable attachment handles and
   process-subject records;
2. a managed sandbox/firewall runtime with signed or otherwise independently
   authenticated policy-attachment exports;
3. a creator-operated isolated-host design whose launch boundary makes policy
   attachment independently observable and fail-closed.

## Evidence separation

A collector may copy authoritative raw bytes from the selected provider and a
normalizer may deterministically parse them. Neither component may create its
own synthetic "raw" input and then attest that its normalized output proves the
target behavior. Such a self-attesting normalizer collapses observation and
assertion into the same trust domain and must be rejected.

For each domain, the selected design must keep these roles inspectable:

- provider: produces evidence independently of the target's assertion;
- collector: copies the provider output without semantic rewriting;
- normalizer/validator: parses and checks the retained bytes;
- creator receipt: authorizes the exact provider, collector source, and hashes.

## Current enforcement limitation

This separation is a design proposal, not a property of schema v4. The current
boundary binds collector paths and hashes, while the validator hashes normalized
event objects rather than retained provider bytes. It does not bind an
independent provider identity or raw-artifact byte ranges. A synthetic
self-attesting collector can therefore satisfy the current bootstrap checks.

Closing that gap requires a separately protected schema, receipt-claim,
raw-artifact, and validator revision. Until that revision exists, no local pass
may be described as provider-independent proof.

## Creator decisions required

To adopt this proposal as a binding activation prerequisite, the creator and
protected control plane would need to decide and protect:

1. the selected provider option for each of protocol, network, and policy
   attachment;
2. whether any provider changes the direct route, process identity, privilege
   boundary, or threat model;
3. exact executable/API identity, version, source or binary hash, invocation,
   privileges, and output format for each provider;
4. the raw-byte retention and redaction rules, including how request IDs,
   timestamps, boot identity, process births, and policy hashes are bound;
5. the independent observer/trust principal for each capture;
6. the fail-closed behavior when capture is incomplete, lossy, ambiguous, or
   unavailable.

The choice should be recorded in protected governance before implementation
and should remain replaceable by a later protected decision. No option in this
proposal is selected by default.

## Recommendation

Recommended next protected decision: defer pass-producing collector
implementation, select and ratify the underlying evidence providers, revise the
machine-enforced evidence contract, and then design each collector as the
smallest content-addressed adapter to retained provider output.

Until those decisions exist, keep A0, keep WP-0001 blocked, keep the protected
repo MCP entry disabled, and keep Unity tool invocations at zero.
