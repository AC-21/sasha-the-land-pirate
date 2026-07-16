# WP-0001 component-signature context recheck

Status: **A0 / read-only observation / non-activation**

Observation window: `2026-07-16T14:11:07Z` through
`2026-07-16T14:20:56Z`.

This record narrows one pre-A1 blocker. It does not ratify a signing tuple,
activate WP-0001, authorize a Unity-family process, or authorize a Unity tool
call.

## Result

The same current component bytes produced opposite signing results in two
execution contexts:

| Execution context | Strict `codesign` | Bundle `spctl` |
| --- | --- | --- |
| Current Codex desktop task's default workspace sandbox | `invalid signature` | internal Code Signing subsystem error |
| User-approved read-only host diagnostic outside that workspace sandbox | valid on disk; satisfies Designated Requirement | accepted; Notarized Developer ID |

This is an execution-context split, not evidence of a temporal repair. The root
cause is not proven. Signature verification remains an activation gate.
Protected authority must select and machine-bind exactly one route:

- run verification inside the exact A1 principal, filesystem view,
  sandbox/policy boundary, and process ancestry that will own the client,
  relay, and Editor; or
- ratify a distinct closed-schema external boundary that explicitly redefines
  verification authority and independently authenticates mappings to every
  exact A1 component path, vnode, hash, process birth, bundle resource, and
  policy record.

The current schema and live validator do not machine-bind that verifier
execution context. Their signing evidence binds the component tuple, but not
the verifier's own executable, PID, UID, parent, sandbox/policy attachment, or
filesystem view. A protected contract revision or distinct protected external
verifier boundary with the closed-schema mappings above is therefore
recommended before activation.

This A0 record does not amend schema v4 or current activation authority. Schema
v4 cannot machine-prove the observed context gap, so a v4 pass must not be
described as closing it. The creator must ratify a successor or explicitly
reject it in favor of an exact protected alternative before relying on this
dimension; until then the runbook remains fail-closed at A0.

## Preserved sandbox failure

Inside the current Codex workspace sandbox, these system commands failed:

```text
/usr/bin/codesign --verify --strict --verbose=4 /Applications/ChatGPT.app
/usr/bin/codesign --verify --strict --verbose=4 /Applications/ChatGPT.app/Contents/Resources/codex
/usr/bin/codesign --verify --strict --verbose=4 /Users/sasha/.unity/relay/relay_mac_arm64.app
/usr/sbin/spctl --assess --type execute --verbose=4 /Applications/ChatGPT.app
```

The three `codesign` checks returned exit `1` and `invalid signature (code or
signature have been modified)`. The `spctl` check returned exit `1` and
`internal error in Code Signing subsystem`.

The same sandbox result was reproduced against a pristine `ChatGPT.app` mounted
read-only from the verified official OpenAI disk image: its app and nested
client returned `invalid signature`, and its app-bundle Gatekeeper assessment
returned the same internal error.

No repair or replacement action was performed. The observed client and relay
executable SHA-256 values remained unchanged. This record does not claim a
complete pre/post manifest for every bundle file or metadata attribute.

## Clean OpenAI reference

The official macOS download linked from
<https://openai.com/codex/for-work/> resolved to:

`https://persistent.oaistatic.com/codex-app-prod/Codex.dmg`

The artifact was downloaded only to `/private/tmp`, verified, and mounted
read-only. It was never installed or launched.

| Field | Observation |
| --- | --- |
| HTTP content type | `application/x-apple-diskimage` |
| HTTP content length | `615797208` bytes |
| HTTP last modified | `2026-07-16T05:45:38Z` |
| DMG SHA-256 | `5a2ab9689f4ba38fcb135565246d5ca2f124d539336a0a32afcdb72040d21466` |
| `hdiutil verify` | valid |
| Image CRC32 | `100FF90A` |
| Mounted mode | read-only, no-browse |
| App | `ChatGPT.app` |
| Bundle identifier | `com.openai.codex` |
| Version / build | `26.707.91948` / `5440` |
| App TeamIdentifier | `2DC432GLL2` |
| App CDHash | `3972f0bc0675d00e71d20be5009b5b5c22b3d905` |
| Bundled client size | `260456864` bytes |
| Bundled client SHA-256 | `bdcb530615d44fcc7b35d12fe00f30c3025c25fc22a21193591dcdb064304385` |
| Bundled client identifier | `codex` |
| Bundled client TeamIdentifier | `2DC432GLL2` |
| Bundled client CDHash | `398aca71386fdc89bd7a9e30cceefe36764c3809` |

In the user-approved read-only host diagnostic, the clean app and nested client
passed strict verification. Gatekeeper accepted the clean **app bundle** as
`Notarized Developer ID`. A direct `spctl` assessment of the nested command-line
client returned `rejected (the code is valid but does not seem to be an app)`;
that expected non-app result was not treated as a signature failure.

## Installed component comparison

No installed component was invoked.

### ChatGPT/Codex

Installed paths:

- `/Applications/ChatGPT.app`
- `/Applications/ChatGPT.app/Contents/Resources/codex`

The installed client SHA-256 remained
`bdcb530615d44fcc7b35d12fe00f30c3025c25fc22a21193591dcdb064304385`.
Its version, build, TeamIdentifier, and CDHashes matched the clean reference.

In the user-approved read-only host diagnostic:

- the app and nested client passed strict verification;
- Gatekeeper accepted the **app bundle** as `Notarized Developer ID`;
- `diff -rq` between the clean read-only app and installed app reported no
  ordinary tree-content differences.

The `diff` result does not bind extended attributes, ACLs, ownership,
timestamps, resource metadata, or an earlier state of either tree.

### Unity relay

Installed path:

`/Users/sasha/.unity/relay/relay_mac_arm64.app`

| Field | Observation |
| --- | --- |
| Executable SHA-256 | `e52d9dc5380297456dc9ae168bdc981e7344651e653b73424dd4bc88df26eaf1` |
| Executable size | `65843264` bytes |
| Bundle identifier | `com.unity.ai.assistant.relay` |
| TeamIdentifier | `9QW8UQUTAA` |
| CDHash | `f689b76b8cf9b5b54f11af8571f37acb91abac0b` |

The executable SHA-256 matches `pre-a1-readiness-20260716.json`. In the
user-approved read-only host diagnostic, the relay bundle and executable passed
strict verification, and Gatekeeper accepted the **relay app bundle** as
`Notarized Developer ID`. The relay was not launched.

## Activation consequence

Neither context may be substituted for the required A1 proof:

- the host-context passes do not prove verification inside the future A1
  principal and policy boundary;
- the workspace-sandbox failures cannot be dismissed as corrupt bytes, because
  the pristine read-only OpenAI image fails there too;
- signer metadata displayed by `codesign -d` cannot replace a successful strict
  verification in the authoritative activation context.

The creator-controlled activation capture must use the protected selected
verification route and bind the exact system commands, raw output, exit status,
command path, component path and SHA-256, signing tuple, timestamp, process
identity, and applicable policy hashes. Any failure or context mismatch fails
closed.

That rule is not yet fully machine-enforced. A protected schema/collector
successor or distinct closed-schema external boundary would need to bind the
verifier's executable, PID, UID, parent/process ancestry, sandbox/policy
attachment, filesystem view, and policy hashes. This A0 note recommends that
disposition but does not enact it. Until the creator protects a decision, a
host-context pass cannot be described as closing this blocker. No route is
selected merely by this A0 observation.

`CODE-IDENTITY-CONTEXT-PROVIDER-PROPOSAL.md` defines the proposed fourth raw
provider domain and exact machine-enforcement successor. It is also A0-only and
unratified.

This record does **not** close:

- creator ratification of the exact client, relay, and Editor signing tuples;
- Unity seat and organization/project linkage;
- the physical quarantine and network-policy boundary;
- the protected creator project seed;
- ratification of the proposed MCP allowlist;
- raw evidence-provider contract revision and collector authorization;
- the A1 boundary manifest, activation evidence, and activation receipt.
