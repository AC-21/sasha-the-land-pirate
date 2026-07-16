# Lean A1 local-development successor

Status: **creator-ratified / packet accepted / A1 active under separate protected activation**

Date: 2026-07-16

This successor creates a practical alternative to the current WP-0001
activation ceremony for day-to-day local game development. Its third distinct
receipt now activates A1 for exact WP-0003 repository/bootstrap work. It still
does not itself authorize a Unity call, replace WP-0001 completion evidence, or
expand authority beyond the sealed local boundary.

The active state is effective only when the complete activation tree is
contained in protected `main`. A pull-request branch may encode that future
state for validation but remains non-executable before protected merge.

## Why this exists

The original A1 boundary grew into a certification regime: detached Git
repositories, disposable OS principals, raw protocol collectors, process/FD
graphs, code-signing context providers, and schema-v5 authority transactions.
Those controls are disproportionate to a solo, local Unity prototype and delay
the actual purpose of the repository: building and testing the game.

Historical WP-0001 evidence and deviations remain immutable. This successor
uses a new packet instead of rewriting the sealed WP-0001 contract.

## Active ratified authority

`A1-LOCAL-DEV` means one bounded local-development packet may:

- work on a non-`main` Git branch in the durable repository;
- create and edit the Unity project, C# code, tests, technical scenes, UI,
  prototype assets, and supporting build/validation tools inside packet paths;
- interact with an already-open Unity Editor only through the creator-approved
  `UNITY-MCP-EXTERNAL` route;
- enter and exit Play Mode, execute project tests, inspect the console, capture
  screenshots, and create or modify project objects through Unity MCP;
- create branches, checkpoints, commits, pushes, and protected pull requests.

The initial Unity project/CI change remains creator-merged. Status-gated
auto-merge may be enabled for later implementation only after WP-0003 CI exists
and its required check is added to branch protection.

Under exact WP-0003 activation, Unity `6000.5.4f1` is the authorized initial
local-development Editor because it is already installed. This is not
production-engine ratification and does not waive later native-build, Mac
performance, package-compatibility, or rollback gates. IL2CPP, full Xcode, and
the standalone .NET SDK are later proof requirements rather than A1 entry
conditions.

## Activation minimum

A creator-issued protected activation receipt must bind one compact local
boundary manifest. The validator proves durable Git facts and exact byte/hash
bindings; the creator receipt attests transient clean-checkout and visible
Unity state. Together they establish:

1. a clean, valid `agent/*` activation branch whose creator-attested initial
   head is the durable checkpoint and receipt `accepted_commit`, with that
   checkpoint in `origin/main` ancestry; the external-protected receipt
   separately binds the exact manifest bytes, and protected merge materializes
   the active state;
2. the exact repository root and exact `Game` project path;
3. a pre-change Git checkpoint;
4. Unity `6000.5.4f1` as the authorized development Editor and `Game` as the
   intended project path;
5. Unity MCP authority is conditional on the first-use gate while the project is
   bootstrap-pending;
6. the exact first-use preconditions for seat/linkage, Bridge, Codex approval,
   and target matching;
7. no committed secrets and no requested credential, account, license, purchase,
   publishing, or release authority;
8. the allowed and denied action sets below; and
9. the exact packet contract, foundation hashes, and activation receipt.

No raw protocol bytes, packet captures, process ancestry, FD graph, code-signing
attestation, disposable OS user, private HOME, schema-v5 provider, or five-minute
receipt window is required.

This intentionally permits A1 to begin before the Unity project exists, so the
boundary does not recreate the circular setup blocker it replaces.

The manifest branch is an activation attestation snapshot, not a perpetual
work branch. After activation enters protected `main`, each implementation pass
starts from a fresh protected-main checkpoint on a new valid `agent/*` branch
inside the unchanged reservation. Automatic deletion of the merged activation
PR branch therefore does not delete or recreate the receipt-bound checkpoint.

## First Unity MCP use gate

A1 activation authorizes repository/bootstrap work. It does not itself
authorize a Unity tool call. Before the first Unity MCP call in a session, the
agent must establish from the creator and visible Unity state that:

- the creator has created and opened exactly `Game`;
- the creator confirms the licensed Editor is operating;
- Bridge is running;
- Codex is selected and the connection is approved;
- the displayed target is exactly the repository `Game` path; and
- the requested call falls inside the allowed actions below.

Failure or drift stops Unity calls but does not invalidate unrelated repository
work already permitted by A1.

## Allowed actions

Inside declared packet paths:

- edit source and project files;
- create scenes, prefabs, materials, technical assets, tests, and editor tools;
- use Unity MCP project/object/file operations;
- enter/exit Play Mode and run project tests;
- read logs and capture screenshots;
- import assets already present in the repository;
- run local non-destructive validation;
- commit, push, and open protected PRs.

## Creator-gated or denied actions

Agents may not:

- invoke Unity Hub, Editor, Unity CLI, or batchmode directly;
- install or upgrade Unity, packages, SDKs, external tools, or dependencies
  without a new explicit creator instruction;
- change Unity accounts, seats, organizations, licenses, billing, purchases, or
  cloud-service settings;
- expose, request, copy, or commit credentials or secrets;
- publish a build, create a release, deploy, monetize, or contact third parties;
- rewrite Git history, force-push, delete protected branches, bypass required
  checks, or weaken repository protection;
- directly write outside the repository; Editor-owned license, package-cache,
  import-cache, and log writes caused by the creator-opened project remain
  allowed but non-importable;
- merge constitutional, governance, credential, dependency, save-migration,
  publishing, or release changes without a fresh creator review.

## Git and rollback

- Every implementation pass starts from a clean checkpoint.
- `main` remains protected and receives changes only through pull requests.
- Unity-generated caches stay ignored.
- A failed pass is reverted by ordinary Git reversal or branch deletion; no
  history rewrite is required.
- Existing player saves do not exist. Any future save-format change remains
  separately creator-gated.

## Accepted packet

`WP-0003` is the local-development alternative. WP-0001 remains accepted
historical evidence and WP-0002 continues to depend on WP-0001 completion.
WP-0003 initially covers the Unity project skeleton, deterministic core and
non-persisting save interfaces, tests, and technical scenes. It does not itself
unblock the gameplay packet. Production content, release, store, monetization,
and background rollout remain out of scope.

## Ratification and activation record

The authenticated owner comment on PR #18 ratifies the exact proposal and
packet contract. After its squash merge, the owner confirmation on PR #19 binds
the byte-identical proposal artifacts to durable protected main commit
`bf335654e57c9c300060d5e8bdcf5795f0462c62`. That PR #19 comment is the direct
protected source for two separate sealed receipts: `RR-D0052-20260716`
ratifies the constitutional successor and `RR-WP0003-ACCEPT-20260716` accepts
WP-0003. Both comments preserve the explicit delegation disclosure. The owner
activation comment on PR #20 binds the compact manifest, reservation,
foundation state, protected checkpoint, conditional Unity first-use gate, and
claims `A1-LOCAL-BOUNDARY-VERIFIED` plus `ACTIVATE-A1-WP-0003`. The distinct
sealed `RR-WP0003-ACTIVATE-20260716` receipt activates WP-0003 as the sole A1
packet when PR #20 enters protected `main`.
