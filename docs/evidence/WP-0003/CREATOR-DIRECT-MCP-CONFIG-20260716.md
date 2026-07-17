# Creator direction capture — project-scoped direct Unity MCP

Status: **A0 / unsealed / control-plane only**

Captured at `2026-07-17T01:47:28Z` from the authenticated creator
conversation while preparing the post-import WP-0003 handoff.

## Creator directions

> Yes, direct MCP replaces Gateway.

> ok we are approved lets keep building

> computer restarted. can you keep going?

In the immediate context of the already selected `UNITY-MCP-EXTERNAL` route,
these directions authorize a bounded proposal to enable the repository's
project-scoped Codex MCP entry for the exact canonical project:

`/Users/sasha/Documents/Codex/sasha-the-land-pirate/Game`

The configured executable is the already installed relay at
`/Users/sasha/.unity/relay/relay_mac_arm64.app/Contents/MacOS/relay_mac_arm64`.
At capture it was a regular executable of `65,843,264` bytes with SHA-256
`e52d9dc5380297456dc9ae168bdc981e7344651e653b73424dd4bc88df26eaf1`.
No installation, upgrade, account, seat, license, package, billing, or global
Codex MCP configuration change is part of this proposal.

Fresh read-only verification completed at `2026-07-17T01:52:55Z` in the
user-approved host context. Strict `codesign` verification reported both the
relay bundle and its exact executable as valid on disk and satisfying their
Designated Requirements. Gatekeeper accepted the bundle as `Notarized
Developer ID`. The default workspace sandbox still produces the previously
documented false-negative signature result for signed apps; that context is not
presented as authoritative host verification.

## Fail-closed boundary

This unsealed capture is not protected packet authority and does not satisfy a
Unity acceptance test or the per-session first-use gate. Loading the config or
establishing a transport connection is not authorization for a tool call. The
host restart invalidated the earlier Editor PID, socket, and Bridge receipt.
The client-side MCP allowlist exposes only `Unity_ReadConsole`; broader tools,
including `Unity_RunCommand`, remain unavailable until a separate reviewed
change deliberately expands the list.

Candidate `.codex/config.toml` size is `458` bytes and SHA-256 is
`c678b7973d453f83b55ab4fb147fa0a32dab8f31c56ac748df53af239f327555`.

Before any Unity MCP call, the creator must freshly open exactly the canonical
`Game` project, confirm the licensed Editor is operating, run Bridge, select and
approve Codex, and confirm that the displayed target is the exact canonical
path. The agent must reconfirm that the requested action is allowed. The first
call remains the read-only `Unity_ReadConsole` action; failure or drift stops
all Unity calls.

The `.codex/config.toml` edit is outside WP-0003's implementation reservation
and is proposed solely under the `AGENTS.md` rule permitting an explicit,
creator-directed, bounded A0 documentation/control-plane edit. It must enter
protected `main` through its own reviewed pull request. It authorizes no
gameplay, production content, dependency, publishing, release, credential, or
self-integration work.
