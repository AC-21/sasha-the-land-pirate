# WP-0001 evidence index

Status: **A0 / blocked**. Nothing in this directory activates WP-0001 or
authorizes a Unity tool call.

## Current records

- [`pre-a1-readiness-20260716.json`](pre-a1-readiness-20260716.json) — secret-free
  host/toolchain/route snapshot and the preserved deviation ledger.
- [`UNITY-MCP-STATIC-SECURITY-AUDIT-20260716.md`](UNITY-MCP-STATIC-SECURITY-AUDIT-20260716.md)
  — source-level audit of the installed Unity Assistant direct-MCP path.
- [`RAW-CAPTURE-PROVIDER-DECISION.md`](RAW-CAPTURE-PROVIDER-DECISION.md) —
  A0-only proposal separating the unresolved protocol, network,
  policy-attachment, and code-identity-context evidence-provider decisions.
- [`CODE-IDENTITY-CONTEXT-PROVIDER-PROPOSAL.md`](CODE-IDENTITY-CONTEXT-PROVIDER-PROPOSAL.md)
  — exact A0-only proposal for binding signing commands, verifier identity,
  policy attachment, filesystem view, and raw outputs through a fourth provider
  domain.
- [`A1-MCP-ALLOWLIST-PROPOSAL.md`](A1-MCP-ALLOWLIST-PROPOSAL.md) — A0-only,
  source-grounded proposal for the exact observation-only client allowlist and
  its phase-gated expansion rule.
- [`COMPONENT-SIGNATURE-RECHECK-20260716.md`](COMPONENT-SIGNATURE-RECHECK-20260716.md)
  — read-only recheck showing that identical current components fail signing
  checks inside the Codex workspace sandbox but pass in a user-approved
  read-only host diagnostic, exposing an unbound verifier-context activation
  gate.
- [`CREATOR-A1-ACTIVATION-RUNBOOK.md`](CREATOR-A1-ACTIVATION-RUNBOOK.md) —
  creator-operated, fail-closed activation procedure.
- [`POST-A1-IMPLEMENTATION-SEQUENCE.md`](POST-A1-IMPLEMENTATION-SEQUENCE.md) —
  bounded implementation order to use only after protected A1 activation.

The activation contract is machine-checked by
`a1-boundary-manifest.schema.json`,
`wp0001-a1-activation-evidence.schema.json`, and
`wp0001-a1-evidence-record.schema.json`. The creator-side
`validate_wp0001_a1_live.py` verifies the actual detached candidate and
principal boundary without starting Unity. After the creator has independently
started the exact route, `validate_wp0001_mcp_live.py` reads its OS/process,
config, discovery, and socket state without starting Unity or MCP. Fixed raw
protocol/listener/probe captures are parsed by `validate_foundation.py`; copied
summary facts or blank command files are insufficient. Those raw captures must
come from the exact source-hashed protocol, network, and policy-attachment
collectors authorized by
`AUTHORIZE-WP0001-RAW-COLLECTORS`. The collectors are deliberately not yet
implemented. The proposed fourth code-identity-context collector and provider
are not present in schema v4 and are not covered by that authority claim.
`RAW-CAPTURE-PROVIDER-DECISION.md` recommends selecting independent
raw-evidence providers and revising the machine-enforced evidence contract
before pass-producing collector implementation. That recommendation is not yet
a protected gate; schema v4 still cannot prove provider independence. A1
remains blocked.

The separate `inspect_wp0001_toolchain_static.py` collector can diagnose the
contracted Hub, Editor, Mac IL2CPP, Xcode, .NET, Rosetta, and protected-project
package tuple using bounded file reads only. Its
`wp0001-static-host-toolchain-observation` output is always A0,
`activation_authority: false`, and `activation_evidence_eligible: false`.
It never starts Unity-family or external processes and cannot replace creator
screenshots, successful IL2CPP build evidence, the physical quarantine, or the
activation receipt. Do not store its output beneath `a1-activation/`; the
collector rejects that destination.

## Evidence law

- Preserve failed, aborted, and deviating attempts.
- Commit no secret values, license material, auth tokens, or uncropped account
  identifiers.
- Recalculate every hash after a protected-main change.
- Do not reuse a prepared candidate clone after its approved base changes.
- A creator-issued packet-activation receipt must bind the exact physical
  boundary and evidence bytes before implementation begins.
