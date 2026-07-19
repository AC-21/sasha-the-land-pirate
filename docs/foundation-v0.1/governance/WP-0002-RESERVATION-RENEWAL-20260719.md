# WP-0002 time-only reservation renewal

Status: **creator-authorization required / control-plane only**

Renewal: `A1B-WP-0002-RESERVATION-RENEWAL-20260719`

Receipt: `RR-WP0002-RESERVATION-RENEWAL-20260719`

## Purpose

WP-0002 reached its fixed `2026-07-19T16:06:11Z` reservation expiry while
the protected VGR-13 implementation pull request was completing validation.
This renewal permits the already accepted Last Bearing packet to continue
through `2026-08-19T16:06:11Z` without changing its product contract, paths,
domains, tools, save boundary, merge policy, or release authority.

The renewal replaces only the active lease ID, fencing token, and expiry. The
prior reservation remains preserved in the boundary record, and the current
boundary SHA-256 is retained as the renewal predecessor.

## Released WP-0003 check

`wp0003-core` protects the immutable WP-0003 technical-sandbox proof. WP-0003
is released and its check is no longer required by branch protection. Running
that frozen exact-file-closure check automatically on WP-0002 changes creates
a false failure whenever authorized Last Bearing files exist.

The workflow therefore becomes manual-only. A manual run checks out the exact
accepted WP-0003 proof commit
`b6b283fd63ab54fed5cd9b6dc6ac78a166cc5bb5`; its historical verifier and
closure are not weakened or rewritten.

## Materialization boundary

The control pull request must contain one Stage-1 commit with the exact renewal,
fail-closed validator/tests, and manual-only historical workflow, followed by
one receipt-only commit. The external protected creator comment must bind the
Stage-1 commit, tree, patch SHA-256, changed-path manifest, unchanged packet
contract, previous and renewed reservation identities, and exact expiry.

Because base-owned `wp0002-policy` intentionally rejects active-to-active
control-plane edits, only that check may be temporarily nonrequired for this
exact receipt-bound pull request. `validate` and `wp0002-core`, strict
up-to-date pull requests, admin enforcement, conversation resolution, linear
history, empty bypass allowances, no force push/deletion, and squash-only merge
remain. `wp0002-policy` must be restored and verified immediately after squash
before any implementation pull request proceeds.

## Failure boundary

- The renewal is non-executable until its exact sealed creator receipt exists.
- Any contract, path, domain, tool, save, credential, or release-policy change
  fails closed.
- Any Stage-1 Git fact, creator-comment, receipt, or current-file hash drift
  fails closed.
- The renewal expires at `2026-08-19T16:06:11Z`; it is not self-renewing.
- No prior receipt, evidence, decision, or boundary history may be deleted or
  rewritten to make this renewal pass.
