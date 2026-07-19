# WP-0002 durable checkout successor

Status: **candidate protected control transaction / non-executable until merged**

Date: 2026-07-19

Amendment: `A1B-WP-0002-DURABLE-CHECKOUT-SUCCESSOR-20260719`

Receipt: `RR-WP0002-DURABLE-CHECKOUT-SUCCESSOR-20260719`

## Purpose

This forward-only amendment changes only the active durable checkout used for
WP-0002 development and its exact Unity `Game` child. The previous activation
and delegated-operator paths remain historical facts in the retained boundary,
receipts, captures, and Git history.

The successor target is:

- repository: `/Users/sasha/Documents/Sasha the Atomic Land Pirate/Development/sasha-the-land-pirate`;
- Unity project: `/Users/sasha/Documents/Sasha the Atomic Land Pirate/Development/sasha-the-land-pirate/Game`;
- remote: `https://github.com/AC-21/sasha-the-land-pirate`.

The immutable WP-0002 contract remains
`ce03ba29c00cec0235bd90c8044237f3286980ccfd7fe9a685aaa2a1e91e75aa`.
The reservation, fencing token, allowed paths, domains, Unity tool allowlist,
dispatcher restriction, dependency boundary, save boundary, branch policy,
and denied actions are unchanged.

The metadata-only capture at
`docs/evidence/WP-0002/checkout-successor/scope-capture.json` binds the real,
non-symlink repository and `Game` paths, independent Git directory, exact
remote, protected-main commit/tree equality, clean index, excluded creator
settings hashes without their bytes, retained gate-report counts, Editor
version/readiness, and the exact Unity MCP project root.

## Exact effect

The new `active_checkout_successor` projection becomes the sole lexical root
for new WP-0002 implementation after this receipt-bound control squash reaches
protected `main`. The original `repository` and `unity.project_path` fields
remain unchanged activation history. They are not an alternative active
checkout after the successor becomes effective.

The existing delegated local Unity operator gains no action. Its visible UI
route, client identity, application bindings, and all denial rules remain
unchanged; only its active repository and project target follow the successor.
This amendment does not authorize identity impersonation, a shell or batchmode
Unity route, writes outside the active repository, a package change, a scene or
project-setting change, a dependency, or a direct merge.

## Authority and materialization

The creator explicitly authorized Codex in the active development task to
amend the WP-0002 boundary so the durable project can be stewarded without an
obsolete checkout path blocking ordinary work. A protected GitHub owner
comment must bind the exact Stage-1 base, commit, tree, patch, changed paths,
old and new roots, previous boundary hash, unchanged packet contract, receipt
path, and the single bounded `wp0002-policy` exception.

The comment body is canonical JSON derived from those exact Git facts. The
sealed receipt stores that projection and binds its SHA-256 and GitHub URL.
The protected validator recomputes the projection on the candidate, requires
the named receipt file, proves Stage 1 is the sole child of the exact base, and
proves Stage 2 is its one-file receipt child (including GitHub's tree-identical
synthetic merge wrapper when present). After squash, it requires one unique
first-parent receipt introduction on protected `main`, the exact combined path
set, the durable projection, and the receipt-bound blobs at that introduction;
later edits do not silently rewrite the historical transaction.

Live GitHub authority is a separate blocking release-controller gate, not an
offline-validator claim. Before the temporary exception or merge, the
controller must authenticate the comment as an unedited `AC-21` owner comment
with the exact stored body, and observe successful `validate` and
`wp0002-core` check runs from app `15368` on the exact final head. It must
observe the exact protection state before and during the exception, then the
restored three-check state within 600 seconds after squash. Those live facts
must be reported with the release outcome; a declaration inside the receipt is
not evidence that they occurred.

Materialization is two local commits before squash:

1. Stage 1 contains this record, the append-only boundary projection, schema,
   validator, current-status documentation, and work-packet boundary hash. It
   contains no successor receipt.
2. Stage 2 is the direct child that adds only the named sealed creator receipt,
   whose exact artifact map binds every Stage-1 file and the authenticated
   owner comment.

The base-owned policy canary is expected to reject an active-to-active
foundation mutation. For this exact final head and patch only, the creator may
temporarily make only `wp0002-policy` nonrequired. `validate` and `wp0002-core`
from GitHub Actions app `15368` remain required. Strict pull-request, admin,
conversation-resolution, and linear-history enforcement remain; bypass
allowances, push restrictions, and repository rulesets remain empty; force
push and deletion remain disabled; squash remains the only merge method.

Codex may transmit the creator-delegated squash only after the two retained
checks pass and the exact final head, tree, patch, paths, and receipt are
reviewed. It must restore the exact three required checks immediately after
merge and verify the protected branch before any implementation proceeds.
The exception expires on any head or patch change and is not reusable.

## Failure boundary

- Before merge, the successor is non-executable and the previous active path
  remains authoritative.
- Any receipt, comment, file, hash, check, protection, remote, or target drift
  fails closed.
- A failed or closed control PR changes no authority.
- No prior receipt, report, capture, verifier, schema, or evidence file may be
  deleted or rewritten to make this successor pass.
- WP-0002 implementation stops at the existing reservation expiry unless a
  separately valid renewal exists.
