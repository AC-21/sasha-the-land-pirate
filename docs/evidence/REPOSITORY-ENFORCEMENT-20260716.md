# Repository enforcement snapshot

Status: **A0 / external-state observation / non-activation**

Observed at: `2026-07-16T15:24:01Z`

Repository: `AC-21/sasha-the-land-pirate`

Observed `main`: `b388b840f2edef03359e488521147a1a73012b21`

This snapshot records GitHub repository settings after the initial control-plane
hardening. It grants no game, Unity, packet, receipt, or autonomy authority.
GitHub settings can drift; future agents must verify live state rather than
treating this file as enforcement.

## Main protection

Observed branch protection:

- pull requests required;
- strict up-to-date status checks required;
- required check `validate`, GitHub Actions app ID `15368`;
- required check `Cursor Approval Agent: Pull Request Approver`, Cursor app ID
  `1210556`;
- administrators covered;
- stale reviews dismissed;
- required approving human-review count `0`;
- conversation resolution required;
- linear history required;
- force pushes disabled;
- branch deletion disabled;
- branch locking disabled;
- signed commits not required.

No repository rulesets were present. Main protection is provided by the branch
protection object above.

## Merge behavior

Observed repository merge settings:

- squash merge enabled;
- merge commits disabled;
- rebase merge disabled;
- auto-merge enabled;
- source branch deletion after merge enabled.

Codex may therefore queue an approved in-scope PR with squash auto-merge. GitHub
must wait until both required checks pass against an up-to-date branch.

If either required app does not report, reports failure, or becomes unavailable,
the PR remains blocked. Auto-merge capability does not waive repository,
governance, creator, packet, or autonomy requirements.

## Pending-check queue test

The pull request containing this section is the bounded A0 live-test vehicle for
pending-check auto-merge. Its operator must request squash auto-merge while at
least one required check is still pending, then verify from GitHub's external
timeline that:

- an auto-merge enablement event exists before the merge event;
- both exact required checks conclude successfully before the merge event;
- the resulting `main` push passes the Foundation workflow; and
- GitHub deletes the source branch.

This text specifies the test; it does not self-attest the result. The GitHub
timeline, check runs, merge commit, and post-merge workflow remain the
drift-prone external evidence.

## Reproduction

Read-only live checks:

```text
gh api repos/AC-21/sasha-the-land-pirate/branches/main/protection
gh api repos/AC-21/sasha-the-land-pirate/branches/main/protection/required_status_checks
gh api repos/AC-21/sasha-the-land-pirate/rulesets --paginate
gh api repos/AC-21/sasha-the-land-pirate
```

The exact check app IDs were cross-read from successful check runs on PR #15's
tested head commit.

## Drift response

Fail closed and stop automated merging if:

- either required context or app ID changes;
- strict/up-to-date enforcement is disabled;
- administrator enforcement, conversation resolution, or linear history is
  disabled;
- force push or branch deletion becomes allowed;
- merge commits or rebase merges become enabled without protected review;
- auto-merge executes without both required successful checks;
- a ruleset appears whose behavior conflicts with branch protection;
- live main differs from the reviewed merge base.

Repository enforcement protects Git operations. It does not prove the
independent provider, creator receipt, physical quarantine, or A1 activation
boundaries described elsewhere.
