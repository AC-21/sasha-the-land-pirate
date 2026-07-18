# WP-0002 delegated local Unity operator amendment

Status: **candidate protected transaction / non-executable until sealed**

Date: 2026-07-17

This is an append-only amendment to `A1B-WP-0002-LOCAL-DEV`. It corrects the
canonical durable repository and project paths and delegates a small set of
repeatable, visible local Unity UI actions to Codex so ordinary development
does not stop whenever the exact project or trusted Bridge connection must be
opened again. It does not replace or rewrite the activated boundary at SHA-256
`770f46788ab927bc18638851b220b33e89adb3ee4d5dcfd08b82fcb587dbff52`.

The immutable WP-0002 contract remains
`ce03ba29c00cec0235bd90c8044237f3286980ccfd7fe9a685aaa2a1e91e75aa`.
The amendment becomes authority only after the protected-base closure verifier
live-authenticates both AC-21 GitHub comments and the complete three-phase transaction
binds the exact candidate commit, tree, exhaustive changed-file manifest,
final receipt-only head, patch, checks, merge, and protection evidence under sealed
creator-authorization receipt `RR-WP0002-LOCAL-OPERATOR-20260717`, claim
`AUTHORIZE-WP0002-DELEGATED-LOCAL-UNITY-OPERATOR`, and the resulting protected
transaction enters `main`, and a separate evidence-only protected PR places all
three verifier reports in `main`. Before that closure merge, the successor is
non-executable and cannot be used as authority for a local action.

## Exact local target

- Repository: `/Users/sasha/Projects/sasha-the-land-pirate`
- Unity project: `/Users/sasha/Projects/sasha-the-land-pirate/Game`
- Unity Hub: bundle `com.unity3d.unityhub` at `/Applications/Unity Hub.app`
- Unity Editor: bundle `com.unity3d.UnityEditor5.x` at `/Applications/Unity/Hub/Editor/6000.5.4f1/Unity.app`

The only approvable Bridge client is the current receipt-bound Codex identity.
The approval authority is deliberately narrow: the Unity Bridge must visibly
show client label `codex-mcp-client`, publisher `OpenAI OpCo, LLC`, executable
path `/Applications/ChatGPT.app/Contents/Resources/codex`, and state
`Accepted`; the ChatGPT application must separately show bundle
`com.openai.codex`, version `26.707.91948`, and build `5440`.

The earlier read-only host diagnostic recorded the following context-limited
signature observation:

- application `/Applications/ChatGPT.app`, bundle `com.openai.codex`, version
  `26.707.91948`, build `5440`, publisher display `OpenAI OpCo, LLC`, team
  `2DC432GLL2`, and observed CDHash
  `3972f0bc0675d00e71d20be5009b5b5c22b3d905`;
- Bridge executable `/Applications/ChatGPT.app/Contents/Resources/codex`,
  identifier `codex`, team `2DC432GLL2`, observed CDHash
  `398aca71386fdc89bd7a9e30cceefe36764c3809`, executable SHA-256
  `bdcb530615d44fcc7b35d12fe00f30c3025c25fc22a21193591dcdb064304385`,
  with the exact designated requirement recorded in the boundary.

That observation is retained at
`docs/evidence/WP-0001/COMPONENT-SIGNATURE-RECHECK-20260716.md`. It explicitly
does **not** resolve the signing-context gap and the Bridge UI does not prove a
CDHash or designated requirement. This successor selects the visible
OS-publisher/path plus exact application version/build as authority only for
the narrow local Bridge approval click; it does not claim to close the broader
A1 component-signature gate. Any visible-field or version/build drift fails
closed until a new protected receipt and re-attestation exist.

The observed existing linkage identifiers are non-secret boundary identity:

- cloud project `b2f6f654-8c39-4360-bc5e-26a62e50e159`, SHA-256
  `0bc8f812dc0be6e99edcea952518fe09437762f5353d450c76e0e62992ab56e1`;
- organization `unity_2d2aeb94bdf989c70701`, SHA-256
  `9b502c7e60d87721337fb9836a5bcfbaa6624d472e480de7ecbb328bf97d622a`.

Recording these identifiers does not assert that Unity cloud services are
enabled and does not authorize an account, organization, seat, billing, cloud
configuration, or project-linkage change. `Game/ProjectSettings/ProjectSettings.asset`
remains protected and is not part of this amendment. The repository product
name remains **Sasha the Atomic Land Pirate**.

Stage 1 also retains a metadata-only working-tree scope proof collected only
after the preceding feature PR is merged and the canonical checkout is exact
local `main` with `HEAD == origin/main` and a clean index. The collector derives
classification from raw porcelain-v2 bytes: it requires the two protected
creator settings drifts, permits only untracked UUID-named reports for the
three enumerated WP-0002 Unity gate kinds, requires at least one report of each
kind, and rejects every other dirty path or state. Diagnostic retries may
produce uneven report counts; every retained report is preserved and bound by
path and content hash rather than deleted, renamed, or selected by the agent.

## Delegated visible UI actions

Through Computer Use, Codex may repeat only these local actions without the
creator being present for each click:

1. add the exact canonical `Game` project to Unity Hub;
2. open or switch Hub/Editor to that exact project;
3. approve or reapprove the receipt-bound Codex MCP client only when every
   visible Bridge field and the separately visible application version/build
   match the exact tuple above; and
4. inspect visible, non-secret seat, organization, project, Editor, package,
   Bridge, tool, console, and hierarchy state.

This is delegated local operation, not identity impersonation. Codex may
attribute the action as creator-authorized local operation, but may not make a
new creator commitment, communicate externally as the creator, infer new
authority, approve a different client, choose another project, or expand the
action set. It may not mutate Bridge tools or configuration. Visible UI use
does not authorize a shell, CLI, batchmode,
headless, executable, or other direct Unity process invocation.

The original `creator-opened-exact-game-project` first-use record remains
historical activation evidence. For the successor amendment, the exact project
may instead be opened or switched by either the creator or the receipt-bound
delegated local operator; the licensed-Editor, running-Bridge, exact visible
receipt-bound client identity, target match, and requested-call checks still
must all pass.

## Unchanged implementation and denial boundary

The Unity implementation route remains exactly:

- `Unity_ReadConsole`
- `Unity_RunCommand`
- `Unity_ManageEditor`
- `Unity_ManageGameObject`
- `Unity_Camera_Capture`

`Unity_RunCommand` retains its enumerated, content-addressed dispatcher
constraint. Every prior denial remains in force, including installs and
package expansion, account/seat/license/billing changes, credential or secret
access, publishing, third-party contact, Git history rewrite or protection
bypass, writes outside the repository, implementation-time governance edits,
external dependency changes, and direct merge.

## Protected transaction sequence

1. Prepare the amendment, schema, validator, tests, docs, and the WP-0002
   boundary-hash pointer in one candidate commit, with no receipt fabrication.
2. Run the reviewed transaction verifier's provisional `authority` phase. It
   must fetch the
   repository, PR, and comment from GitHub; match repository-owner and comment
   actor numeric IDs, login, type, and `OWNER` association; and bind the exact
   comment ID, URLs, timestamps, UTF-8 body hash, candidate commit/tree,
   deterministic patch, full-tree listing, and exhaustive changed-file
   manifest. A GitHub comment ID is stable but its body is mutable; every later
   phase, including completion and evidence closure, must refetch and reject
   any identity, timestamp, or body drift.
3. Materialize the sealed creator-authorization receipt in a second commit,
   bound to that authenticated comment and candidate commit. The second commit
   may add exactly one regular file: the named receipt. Its artifact-key set
   must equal the complete Stage-1 changed-file set plus the authenticated
   comment key, with no extra or missing key.
4. Because the base-owned `wp0002-policy` workflow correctly rejects every
   active-to-active governance mutation, the creator may make only that context
   temporarily nonrequired for this control PR's exact final head and patch.
   `validate` and `wp0002-core` from GitHub Actions app `15368` remain required;
   strict up-to-date PR enforcement, admin enforcement, conversation
   resolution, linear history, empty bypass allowances, disabled force push,
   disabled deletion, squash-only merging, and the exact empty inherited and
   repository ruleset inventory all remain unchanged.
5. Run the reviewed transaction verifier's provisional `pre-merge` phase.
   Re-read the authenticated
   comment and final protected PR base/head/patch, prove exact receipt-only
   delta, require latest-head `validate` and `wp0002-core` successes from app
   `15368`, and bind normalized plus raw-hash evidence of both the full
   three-check `before` state and exact two-check `during` state. This proves
   snapshots, not uninterrupted absence of a transient change. Each snapshot
   also refetches the repository ruleset inventory and rejects any entry.
6. Only then transmit the creator-delegated squash merge. Restore
   `wp0002-policy`, then run the verifier's `complete` phase: prove the squash
   commit's sole parent and tree, main inclusion, exact restored `after` state,
   and a second authenticated owner completion comment binding the authority,
   pre-merge, merge, and before/during/after evidence hashes. The restored
   `after` capture must occur no later than 600 seconds after the merge
   timestamp. The first implementation PR opened after restoration remains the
   fresh latest-head canary for all three required checks; restoration alone
   is not canary success.
7. The three reports cannot truthfully exist in the receipt-only control PR:
   authority evidence is generated after Stage 1, and completion evidence is
   generated after merge. From the restored protected `main`, open a fresh
   `agent/*` evidence-closure PR that adds exactly `authority.json`,
   `pre-merge.json`, and `complete.json` at the three contract paths as exact
   regular additions and changes nothing else. The base-owned
   `wp0002-policy` workflow must hash-check and execute only its protected-base
   verifier bytes, then live-refetch both owner comments, the control PR, main,
   final-head checks, restored branch protection, and the empty ruleset
   inventory. It must pass `validate`, `wp0002-core`, and `wp0002-policy`
   under the ordinary full protection set and merge by squash. All-absent
   reports mean valid control materialization but non-executable successor;
   any partial set fails closed. Only the protected closure merge makes the
   delegated local-operator successor effective.

The closure workflow's ordinary `GITHUB_TOKEN` cannot read GitHub's classic
branch-protection endpoint. Live closure therefore fails closed unless the
creator has separately provisioned repository Actions secret
`WP0002_PROTECTION_READ_TOKEN` with exactly one-repository, short-expiry,
Administration-read scope. The secret is injected only into protected-base
closure code and used only by the separate protection reader; candidate code
does not execute or receive it. This is opaque control-plane authentication,
not authorization for Codex to read, copy, display, create, rotate, or otherwise
handle credential material.

Step 4 is creator-controlled control-plane configuration, not A1 implementation
authority and not a general protection-bypass permission. It cannot be reused
for another head, patch, PR, check, or purpose. The repository validator proves
structural and content consistency only; it does not claim live GitHub
authentication. Until steps 2–7 complete under
`Tools/Validation/verify_wp0002_local_operator_transaction.py` and all three
reports are validated in protected `main`, the previous boundary remains the
only active authority.
