# Lean A1 local-development successor

Status: **proposed / inactive / creator ratification, packet acceptance, and separate activation required**

Date: 2026-07-16

This proposal creates a practical alternative to the current WP-0001
activation ceremony for day-to-day local game development. It does not activate
A1, authorize a Unity call, replace WP-0001 completion evidence, or change
current authority until a creator-controlled protected receipt ratifies the
proposal, a separate receipt accepts its work packet, and a third receipt
activates the exact local boundary.

## Why this exists

The original A1 boundary grew into a certification regime: detached Git
repositories, disposable OS principals, raw protocol collectors, process/FD
graphs, code-signing context providers, and schema-v5 authority transactions.
Those controls are disproportionate to a solo, local Unity prototype and delay
the actual purpose of the repository: building and testing the game.

Historical WP-0001 evidence and deviations remain immutable. This proposal
creates a new packet instead of rewriting the sealed WP-0001 contract.

## Proposed authority

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

Upon exact WP-0003 activation, Unity `6000.5.4f1` would be authorized as the
initial local-development Editor because it is already installed. This is not
production-engine ratification and does not waive later native-build, Mac
performance, package-compatibility, or rollback gates. IL2CPP, full Xcode, and
the standalone .NET SDK are later proof requirements rather than A1 entry
conditions.

## Activation minimum

A creator-issued protected activation receipt must bind one compact local
boundary manifest. The validator proves durable Git facts and exact byte/hash
bindings; the creator receipt attests transient clean-checkout and visible
Unity state. Together they establish:

1. a clean, valid `agent/*` branch whose creator-attested head equals the real
   activation commit and whose real base/checkpoint exists in
   `origin/main` ancestry;
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

## Proposed packet

`WP-0003` is the local-development alternative. WP-0001 remains accepted
historical evidence and WP-0002 continues to depend on WP-0001 completion.
WP-0003 initially covers the Unity project skeleton, deterministic core and
non-persisting save interfaces, tests, and technical scenes. It does not itself
unblock the gameplay packet. Production content, release, store, monetization,
and background rollout remain out of scope.

## Ratification protocol

The creator must approve the exact proposal commit through an authenticated
GitHub comment. One comment may serve as the protected source for two separate
derived receipts: a `decision-ratification` receipt for the constitutional
successor and a `packet-acceptance` receipt for WP-0003. A later commit may seal
both while leaving activation pending until the compact boundary manifest
exists.
