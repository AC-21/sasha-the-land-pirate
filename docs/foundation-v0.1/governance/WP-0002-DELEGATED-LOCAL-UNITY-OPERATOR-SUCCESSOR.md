# WP-0002 Delegated Local Unity Operator Successor

Status: **pending exact online transaction and protected evidence closure**  
Successor amendment: `A1B-WP-0002-LOCAL-OPERATOR-SUCCESSOR-20260718`  
Transaction: `WP0002-LOCAL-OPERATOR-SUCCESSOR-20260718`  
Receipt: `RR-WP0002-LOCAL-OPERATOR-SUCCESSOR-20260718`

## Purpose

This is a forward-only correction to the unclosed local-operator control
materialized by PR #60. It does not change the WP-0002 packet contract, game
design, Unity action set, project, client identity, installed applications,
tool allowlist, or denied-action boundary. It replaces only the non-executable
transaction route whose original verifier could not load under offline
validation and incorrectly compared historical Stage-1 claims to later
working-tree bytes.

The predecessor receipt
`RR-WP0002-LOCAL-OPERATOR-20260717` remains sealed and byte-identical at
SHA-256
`fb1df094bac7c4b944e1438eff4e761963e7076fb58b69f36ef497939eba8b8e`.
The predecessor boundary remains historically addressable at protected squash
`96002331dc069db5a7bab36baaf359d1b46cc64c`, where its SHA-256 is
`e92e7276dc97478d7412307f43a5f90b60b99256b2a8a7cd5d00626c4f2e0962`.
Nothing in this successor makes that unclosed route effective retroactively.

## Predecessor disposition

The predecessor amendment is `superseded-unclosed-never-effective`.

- PR #60 merged its receipt-bound control and restored branch protection, but
  no protected evidence-closure PR added the complete three-report chain.
- PR #61 attempted an exact inverse. GitHub blocked the merge because deleting
  the sealed receipt violated conversation-resolution and append-only-ledger
  review. The temporary status-check exception was restored, and PR #61 closed
  unmerged.
- The predecessor authority comment, completion comment, patch exception, and
  absent report namespace are nonreusable.
- These predecessor report paths are permanently forbidden:
  `docs/evidence/WP-0002/local-operator-amendment/authority.json`,
  `pre-merge.json`, and `complete.json`.

The sealed predecessor receipt, governance record, verifier, tests, schemas,
and capture artifacts remain historical evidence. Current successor validators
must authenticate those bytes from immutable Git objects rather than assuming
that historical Stage-1 files still equal current working-tree files.

## Exact successor claims

One new protected creator-authorization receipt must carry both claims:

1. `SUPERSEDE-WP0002-LOCAL-OPERATOR-20260717-UNEXECUTED` for predecessor
   amendment `A1B-WP-0002-LOCAL-OPERATOR-20260717`;
2. `AUTHORIZE-WP0002-DELEGATED-LOCAL-UNITY-OPERATOR-SUCCESSOR` for `WP-0002`.

The authenticated owner comment must bind the exact Stage-1 base, commit,
tree, deterministic patch, changed-file manifest, predecessor receipt hash,
predecessor boundary hash, successor receipt path, packet contract, and the
single temporarily nonrequired `wp0002-policy` context. Any head, patch, tree,
comment, identity, receipt, scope-capture, protection, or ruleset drift makes
the authority non-executable.

## Fresh canonical scope proof

The successor uses a new metadata-only capture rooted at
`docs/evidence/WP-0002/local-operator-successor/scope-capture/`. It must bind
base, local `main`, checkpoint, and `origin/main` to exact protected commit
`96002331dc069db5a7bab36baaf359d1b46cc64c`; record the complete canonical
dirty set; preserve creator-owned project settings and packet-generated Unity
gate reports; and retain no creator file bytes. A stale, partial, differently
rooted, or mismatched capture fails closed.

Stage 1 must also commit the exact v2 `capture-protection` result at
`docs/evidence/WP-0002/local-operator-successor/control/protection-before.json`.
That trusted local capture must prove exact-three protection while protected
main is still the Stage-1 base, be produced before the Stage-1 commit and owner
comment, and fall no more than 300 seconds before the authenticated comment's
GitHub `created_at`. Because the comment binds the complete Stage-1 tree and
changed-file manifest, it also binds this exact capture blob. Authority and
pre-merge verification must reject a stale, relabeled, substituted, missing,
or non-Stage-1 protection-before capture.

## Materialization transaction

The successor control remains two commits before squash:

1. Stage 1 contains the complete successor control patch, fresh scope capture,
   and exact committed protection-before capture, with no successor receipt
   and no closure reports.
2. Stage 2 is the single direct child of Stage 1 and adds only
   `docs/foundation-v0.1/ledger/receipts/RR-WP0002-LOCAL-OPERATOR-SUCCESSOR-20260718.json`.

The v2 verifier hash-pins and loads the immutable v1 implementation through a
fresh `ModuleType`, registers it temporarily in `sys.modules`, hashes before
execution, and restores an absent, `None`, or pre-existing entry exactly even
on failure. It validates Stage-1 and final-head evidence from Git commit, tree,
`ls-tree`, blob, parent, patch, and hash facts. Current filesystem bytes never
stand in for historical evidence.

The creator-controlled merge transaction may temporarily make only
`wp0002-policy` nonrequired for the exact final PR head and patch. Throughout
that bounded interval:

- `validate` and `wp0002-core`, both GitHub Actions app `15368`, remain required;
- strict up-to-date, pull-request, admin, conversation-resolution, and linear
  history enforcement remain;
- bypass allowances, push restrictions, and repository rulesets remain empty;
- force push and deletion remain disabled; and
- squash remains the only merge method.

Protection must be restored to exact required checks `validate`,
`wp0002-core`, and `wp0002-policy` within 600 seconds of the squash and captured
before the owner completion comment. The exception is single-use and expires
on any head or patch change.

The control squash begins a deliberate fail-closed quarantine on protected
`main`: without the exact three closure reports, ordinary foundation
validation may remain red because the pre-squash Stage-1/receipt-child topology
is no longer `HEAD`. This is not a release state and grants no authority. The
controller must continue without an unrelated merge or implementation PR,
restore exact-three protection immediately, produce the completion evidence,
and merge only the exact-three-report closure PR. The 600-second bound applies
to protection restoration; the closure sequence is continuous and may not be
paused or used as a general red-main allowance.

## Evidence closure and effectiveness

The control squash alone authorizes no local Unity operation. A second PR must
add exactly these three regular files and no other delta:

- `docs/evidence/WP-0002/local-operator-successor/authority.json`;
- `docs/evidence/WP-0002/local-operator-successor/pre-merge.json`;
- `docs/evidence/WP-0002/local-operator-successor/complete.json`.

That PR must pass all three ordinary required checks. Base-owned
`wp0002-policy` hash-checks the v2 verifier before execution, rejects any
predecessor report injection, refetches both owner comments and the control PR,
and revalidates protected main, exact checks, complete branch protection,
empty rulesets, the squash tree, immutable predecessor bytes, and the report
hash chain. Only the protected squash of that exact closure makes the
successor delegated local-operator boundary effective.

Until then, the creator remains the only party that may perform visible Unity
Hub or Editor setup actions. After closure, the allowed visible Computer Use
actions and all denied actions remain exactly those already described by the
WP-0002 boundary. This successor is not identity impersonation, external
communication authority, direct Unity-process authority, a reusable bypass,
or permission to infer new creator commitments.

## Failure and rollback

- Failure before the control squash leaves protected `main` at the safe
  predecessor-pending state.
- Failure after the control squash but before evidence closure leaves both the
  predecessor and successor non-executable and protected `main` deliberately
  red/fail-closed; restore exact-three protection, permit no unrelated merge,
  and record the failure without deleting history.
- Failure of the closure PR leaves the successor pending. Do not add partial
  reports, loosen the verifier, reuse comments, or resurrect predecessor paths.
- Any later correction must append another explicitly superseding amendment
  and new sealed receipt; it must not delete or rewrite either historical
  receipt.
