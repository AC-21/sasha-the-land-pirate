# WP-0002 Local-Operator Mixed-Reader Recovery

Status: **proposed; non-executable until protected creator receipt and squash**

- Amendment: `A1B-WP-0002-LOCAL-OPERATOR-RECOVERY-20260718`
- Transaction: `WP0002-LOCAL-OPERATOR-CLOSURE-RECOVERY-20260718`
- Receipt: `RR-WP0002-LOCAL-OPERATOR-RECOVERY-20260718`
- Predecessor transaction: `WP0002-LOCAL-OPERATOR-SUCCESSOR-20260718`

## Purpose and limit

This append-only recovery corrects one live-reader assignment in the protected
evidence-closure verifier. The v2 verifier read classic protection and
rulesets with the repository-scoped Administration-read credential, but read
repository merge-method settings with the ordinary Actions credential. That
mixed projection made the otherwise exact PR #64 closure fail closed.

V1, v2, their receipts, their reports, PR #64, and their Git objects remain
immutable. This recovery does not change the WP-0002 packet contract, Unity scope,
allowed actions, denied actions, trusted client identity, creator-owned drift,
or the durable exact-three required-check policy. It grants no A1 authority by
itself.

## Corrected reader assignment

The hash-pinned v3 adapter loads immutable v2 and changes only the live
protection projection:

- the ordinary read-only reader supplies branch identity, repository/owner
  identity, pull requests, comments, check runs, and Git fetch identity;
- the single-repository, short-expiry Administration-read reader supplies
  classic branch protection, repository merge settings, and rulesets; and
- candidate code never receives or executes with the Administration-read
  credential.

Any normalized mismatch fails closed. Diagnostics may list only sorted
normalized field names and must never emit either live or recorded values,
headers, tokens, or raw API bodies.

## Forward-only external control transaction

The protected-base policy has no governance-recovery phase: replaying this
control diff against the base-owned `wp0002-policy` correctly rejects its
workflow, verifier, boundary, schema, and foundation-control changes. The
recovery therefore uses the smallest explicit external control transaction.
Only `wp0002-policy` may be temporarily nonrequired for the exact
content-addressed recovery-control-plus-receipt PR. `validate` and
`wp0002-core`, both from GitHub Actions app `15368`, remain required and must
pass on the exact receipt head. Strict up-to-date PRs, required PRs, enforced
admins, conversation resolution, linear history, empty bypass and push
restrictions, disabled force push and deletion, and squash-only merging remain
unchanged throughout. No direct push, merge commit, rebase merge, or general
bypass is authorized.

The protected creator comment must bind the exact Stage-1 base, commit, tree,
deterministic patch, changed-file manifest, packet contract, retained v2
receipt hash, predecessor boundary hash, recovery receipt path, and the exact
temporary `wp0002-policy` exception. Stage 2 must be the single receipt-only
child of Stage 1. Pre-merge evidence must prove exact-three protection before
the transaction, exact `validate` plus `wp0002-core` protection during it, and
successful final-head runs for those two retained checks.

The recovery becomes installed only when that exact receipt child is
squash-merged through the creator-controlled PR. `wp0002-policy` must then be
restored immediately, the exact-three protection state must be live-captured
within 600 seconds, and the authenticated unedited owner completion comment
must also be created no later than 600 seconds after the merge. A caller-supplied
protection observation timestamp is not sufficient proof of that deadline. No
unrelated PR may merge in between. Installation still authorizes no local Unity
action. The delegated operator remains pending until a fresh PR, without
modifying PR #64, adds exactly the three new v3 recovery reports and all
exact-three checks pass under protected-base v3.

## Evidence replay

The replay PR must add only:

- `docs/evidence/WP-0002/local-operator-recovery/authority.json`;
- `docs/evidence/WP-0002/local-operator-recovery/pre-merge.json`; and
- `docs/evidence/WP-0002/local-operator-recovery/complete.json`.

Those blobs are a new recovery transaction. They bind the recovery Stage-1,
receipt-only child, and repair squash, so the repair squash is also the later
closure base. V3 retains v2 historical bytes and validates the exact
before/during/after protection transition while correcting which trusted
reader supplies repository merge settings during every live protection
recheck.

## Failure state

Before the recovery receipt and protected squash, this proposal is
non-executable. After installation but before the fresh closure merge, the
v2 delegated local operator remains non-executable and protected main remains
in fail-closed quarantine. Any additional mismatch requires another
append-only correction; neither historical route may be edited or deleted.
