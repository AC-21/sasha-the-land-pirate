#!/usr/bin/env python3
"""Verify the WP-0002 local-operator control transaction against live GitHub.

This is a trusted local/controller tool, not a candidate-CI gate.  It performs
read-only GitHub GET requests and local Git object inspection.  In particular,
it never changes branch protection, merges a pull request, posts a comment, or
claims that candidate code may receive an administration token.

The transaction is deliberately split into three evidence phases:

* ``authority`` authenticates the repository owner comment and the exact
  stage-1 Git tree;
* ``pre-merge`` proves the final head is the stage-1 commit plus one receipt
  file and records the before/during protection states; and
* ``complete`` proves the squash tree, restored protection, and an owner
  completion comment chained to the earlier evidence.

The pure builders accept injected GitHub readers so tests need no network. The
evidence-closure path accepts a separate protection reader: candidate code and
the ordinary workflow token never receive the narrowly scoped Administration
read credential required by GitHub's classic branch-protection endpoint.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import subprocess
import sys
import tempfile
import urllib.error
import urllib.request
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path, PurePosixPath
from typing import Any, Mapping, Protocol


REPOSITORY = "AC-21/sasha-the-land-pirate"
OWNER_LOGIN = "AC-21"
TRANSACTION_ID = "WP0002-LOCAL-OPERATOR-20260717"
AUTHORIZATION_CLAIM = "AUTHORIZE-WP0002-DELEGATED-LOCAL-UNITY-OPERATOR"
COMPLETION_CLAIM = "COMPLETE-WP0002-LOCAL-OPERATOR-CONTROL-TRANSACTION"
CONTRACT_SHA256 = "ce03ba29c00cec0235bd90c8044237f3286980ccfd7fe9a685aaa2a1e91e75aa"
RECEIPT_ID = "RR-WP0002-LOCAL-OPERATOR-20260717"
RECEIPT_PATH = (
    "docs/foundation-v0.1/ledger/receipts/"
    "RR-WP0002-LOCAL-OPERATOR-20260717.json"
)
AUTHORITY_EVIDENCE_PATH = (
    "docs/evidence/WP-0002/local-operator-amendment/authority.json"
)
PRE_MERGE_EVIDENCE_PATH = (
    "docs/evidence/WP-0002/local-operator-amendment/pre-merge.json"
)
COMPLETE_EVIDENCE_PATH = (
    "docs/evidence/WP-0002/local-operator-amendment/complete.json"
)
CLOSURE_EVIDENCE_PATHS = (
    AUTHORITY_EVIDENCE_PATH,
    PRE_MERGE_EVIDENCE_PATH,
    COMPLETE_EVIDENCE_PATH,
)
TEMPORARY_NONREQUIRED_CHECK = "wp0002-policy"
REQUIRED_APP_ID = 15368
REQUIRED_CHECKS = ("validate", "wp0002-core")
FULL_REQUIRED_CHECKS = ("validate", "wp0002-core", "wp0002-policy")
MAX_RESTORE_DELAY_SECONDS = 600
GITHUB_API = "https://api.github.com"
GITHUB_WEB = "https://github.com"
GIT_SHA_RE = re.compile(r"^[0-9a-f]{40}$")
SHA256_RE = re.compile(r"^[0-9a-f]{64}$")


class VerificationError(ValueError):
    """Raised when evidence differs from the exact transaction contract."""


@dataclass(frozen=True)
class APIResult:
    data: Any
    raw: bytes
    headers: Mapping[str, str] | None = None


class GitHubReader(Protocol):
    def get_json(self, path: str) -> APIResult: ...

    def get_bytes(self, path: str, *, accept: str) -> APIResult: ...


class LiveGitHubReader:
    """Small read-only GitHub API client for the trusted local controller."""

    def __init__(self, token: str | None = None, *, timeout: int = 30) -> None:
        self._token = token
        self._timeout = timeout

    def _request(self, path: str, *, accept: str) -> APIResult:
        if not path.startswith("/") or "\x00" in path:
            raise VerificationError("GitHub API path must be an absolute API path")
        headers = {
            "Accept": accept,
            "User-Agent": "wp0002-local-operator-transaction-verifier/1",
            "X-GitHub-Api-Version": "2022-11-28",
        }
        if self._token:
            headers["Authorization"] = f"Bearer {self._token}"
        request = urllib.request.Request(
            GITHUB_API + path,
            method="GET",
            headers=headers,
        )
        try:
            with urllib.request.urlopen(request, timeout=self._timeout) as response:
                raw = response.read()
                response_headers = {key.lower(): value for key, value in response.headers.items()}
        except (urllib.error.URLError, TimeoutError, OSError) as exc:
            raise VerificationError(f"GitHub GET failed for {path}: {exc}") from exc
        return APIResult(None, raw, response_headers)

    def get_json(self, path: str) -> APIResult:
        response = self._request(path, accept="application/vnd.github+json")
        try:
            data = json.loads(response.raw.decode("utf-8"))
        except (UnicodeDecodeError, json.JSONDecodeError) as exc:
            raise VerificationError(f"GitHub returned invalid JSON for {path}") from exc
        return APIResult(data, response.raw, response.headers)

    def get_bytes(self, path: str, *, accept: str) -> APIResult:
        return self._request(path, accept=accept)


def canonical_json_bytes(value: object) -> bytes:
    return json.dumps(
        value,
        sort_keys=True,
        separators=(",", ":"),
        ensure_ascii=False,
    ).encode("utf-8")


def evidence_sha256(value: object) -> str:
    return hashlib.sha256(canonical_json_bytes(value)).hexdigest()


def _sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def _artifact_ref(data: bytes) -> dict[str, object]:
    return {"sha256": _sha256(data), "byte_size": len(data)}


def _dict(value: object, label: str) -> dict[str, Any]:
    if not isinstance(value, dict):
        raise VerificationError(f"{label} must be an object")
    return value


def _list(value: object, label: str) -> list[Any]:
    if not isinstance(value, list):
        raise VerificationError(f"{label} must be an array")
    return value


def _string(value: object, label: str) -> str:
    if not isinstance(value, str) or not value:
        raise VerificationError(f"{label} must be a non-empty string")
    return value


def _integer(value: object, label: str) -> int:
    if not isinstance(value, int) or isinstance(value, bool) or value < 1:
        raise VerificationError(f"{label} must be a positive integer")
    return value


def _git_sha(value: object, label: str) -> str:
    result = _string(value, label)
    if GIT_SHA_RE.fullmatch(result) is None:
        raise VerificationError(f"{label} must be a 40-character lowercase Git SHA")
    return result


def _sha256_value(value: object, label: str) -> str:
    result = _string(value, label)
    if SHA256_RE.fullmatch(result) is None:
        raise VerificationError(f"{label} must be a lowercase SHA-256")
    return result


def _exact_keys(value: Mapping[str, object], keys: set[str], label: str) -> None:
    actual = set(value)
    if actual != keys:
        missing = sorted(keys - actual)
        extra = sorted(actual - keys)
        raise VerificationError(
            f"{label} keys differ; missing={missing}, extra={extra}"
        )


def _parse_time(value: object, label: str) -> datetime:
    text = _string(value, label)
    try:
        parsed = datetime.fromisoformat(text.replace("Z", "+00:00"))
    except ValueError as exc:
        raise VerificationError(f"{label} is not an RFC3339 timestamp") from exc
    if parsed.tzinfo is None:
        raise VerificationError(f"{label} lacks a timezone")
    return parsed.astimezone(timezone.utc)


def _timestamp(value: datetime | None = None) -> str:
    instant = (value or datetime.now(timezone.utc)).astimezone(timezone.utc)
    return instant.isoformat(timespec="seconds").replace("+00:00", "Z")


def _safe_path(value: object, label: str) -> str:
    text = _string(value, label)
    if "\\" in text or "\x00" in text:
        raise VerificationError(f"{label} is not a safe POSIX path")
    path = PurePosixPath(text)
    if path.is_absolute() or any(part in {"", ".", ".."} for part in path.parts):
        raise VerificationError(f"{label} is not a safe repository-relative path")
    return path.as_posix()


class GitRepository:
    """Read-only Git plumbing used by the transaction verifier."""

    def __init__(self, root: Path | str) -> None:
        self.root = Path(root).resolve()
        if not self.root.is_dir():
            raise VerificationError("repository path is not a directory")
        bare = self._run("rev-parse", "--is-bare-repository").decode("ascii").strip()
        if bare == "true":
            git_dir = self._run("rev-parse", "--absolute-git-dir").decode("utf-8").strip()
            if Path(git_dir).resolve() != self.root:
                raise VerificationError("repository path is not the exact bare Git directory")
        elif bare == "false":
            top = self._run("rev-parse", "--show-toplevel").decode("utf-8").strip()
            if Path(top).resolve() != self.root:
                raise VerificationError("repository path is not the exact Git top level")
        else:  # pragma: no cover - git itself constrains this response
            raise VerificationError("Git returned an unexpected bare-repository state")

    def _run(self, *args: str) -> bytes:
        env = {
            "PATH": "/usr/bin:/bin",
            "LANG": "C",
            "LC_ALL": "C",
            "GIT_CONFIG_NOSYSTEM": "1",
            "GIT_TERMINAL_PROMPT": "0",
            "HOME": os.devnull,
        }
        result = subprocess.run(
            [
                "/usr/bin/git",
                "-c", "core.hooksPath=/dev/null",
                "-c", "core.quotepath=false",
                "-c", "diff.external=",
                "-C", str(self.root),
                *args,
            ],
            stdin=subprocess.DEVNULL,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            env=env,
            timeout=30,
            check=False,
        )
        if result.returncode != 0:
            detail = result.stderr.decode("utf-8", "replace").strip()
            raise VerificationError(
                f"Git command failed ({' '.join(args)}): {detail or result.returncode}"
            )
        return result.stdout

    def commit(self, sha: str) -> dict[str, object]:
        sha = _git_sha(sha, "commit SHA")
        raw = self._run("show", "-s", "--format=%H%x00%T%x00%P", sha)
        fields = raw.rstrip(b"\n").split(b"\x00")
        if len(fields) != 3:
            raise VerificationError("Git commit metadata has an unexpected shape")
        commit_sha = fields[0].decode("ascii")
        tree = fields[1].decode("ascii")
        parents_text = fields[2].decode("ascii").strip()
        parents = parents_text.split() if parents_text else []
        _git_sha(commit_sha, "resolved commit SHA")
        _git_sha(tree, "commit tree OID")
        for parent in parents:
            _git_sha(parent, "commit parent SHA")
        return {"sha": commit_sha, "tree": tree, "parents": parents}

    def full_tree_listing(self, sha: str) -> bytes:
        return self._run("ls-tree", "-r", "-z", "--full-tree", sha)

    def deterministic_patch(self, base: str, head: str) -> bytes:
        return self._run(
            "diff",
            "--binary",
            "--full-index",
            "--no-renames",
            "--no-color",
            "--no-ext-diff",
            "--src-prefix=a/",
            "--dst-prefix=b/",
            base,
            head,
            "--",
        )

    def blob(self, oid: str) -> bytes:
        _git_sha(oid, "blob OID")
        return self._run("cat-file", "blob", oid)

    def blob_at(self, commit: str, path: str) -> bytes:
        _git_sha(commit, "blob commit")
        safe = _safe_path(path, "blob path")
        return self._run("show", f"{commit}:{safe}")

    def changed_files(self, base: str, head: str) -> list[dict[str, object]]:
        raw = self._run(
            "diff",
            "--raw",
            "-z",
            "--no-renames",
            "--full-index",
            "--no-abbrev",
            base,
            head,
            "--",
        )
        if not raw:
            return []
        fields = raw.split(b"\x00")
        if fields[-1] != b"":
            raise VerificationError("NUL-delimited Git diff is truncated")
        fields.pop()
        if len(fields) % 2 != 0:
            raise VerificationError("NUL-delimited Git diff has an unexpected shape")
        changed: list[dict[str, object]] = []
        for index in range(0, len(fields), 2):
            header = fields[index]
            path_bytes = fields[index + 1]
            try:
                header_text = header.decode("ascii")
                path_text = path_bytes.decode("utf-8")
            except UnicodeDecodeError as exc:
                raise VerificationError("Git diff contains a non-UTF-8 path") from exc
            match = re.fullmatch(
                r":([0-7]{6}) ([0-7]{6}) ([0-9a-f]{40}) ([0-9a-f]{40}) ([A-Z][0-9]*)",
                header_text,
            )
            if match is None:
                raise VerificationError("Git raw diff header is malformed")
            old_mode, new_mode, old_oid, new_oid, status_token = match.groups()
            status = status_token[0]
            path = _safe_path(path_text, "changed path")
            if status not in {"A", "M"}:
                raise VerificationError(
                    f"control transaction forbids Git status {status!r} at {path}"
                )
            if new_mode != "100644":
                raise VerificationError(
                    f"control transaction requires regular mode 100644 at {path}"
                )
            changed.append(
                {
                    "path": path,
                    "status": status,
                    "old_mode": old_mode,
                    "new_mode": new_mode,
                    "old_oid": old_oid,
                    "new_oid": new_oid,
                    "new_blob_sha256": _sha256(self.blob(new_oid)),
                }
            )
        changed.sort(key=lambda item: str(item["path"]))
        if len({item["path"] for item in changed}) != len(changed):
            raise VerificationError("changed-file manifest repeats a path")
        return changed

    def contains(self, ancestor: str, descendant: str) -> bool:
        try:
            self._run("merge-base", "--is-ancestor", ancestor, descendant)
        except VerificationError:
            return False
        return True


def _repository_projection(payload: object) -> dict[str, object]:
    repository = _dict(payload, "GitHub repository")
    owner = _dict(repository.get("owner"), "GitHub repository owner")
    full_name = _string(repository.get("full_name"), "repository full_name")
    if full_name != REPOSITORY:
        raise VerificationError("GitHub response names the wrong repository")
    owner_login = _string(owner.get("login"), "repository owner login")
    if owner_login != OWNER_LOGIN:
        raise VerificationError("repository is not owned by AC-21")
    return {
        "full_name": full_name,
        "id": _integer(repository.get("id"), "repository id"),
        "owner_login": owner_login,
        "owner_id": _integer(owner.get("id"), "repository owner id"),
    }


def _pull_projection(payload: object, number: int, *, authority: bool) -> dict[str, object]:
    pull = _dict(payload, "GitHub pull request")
    base = _dict(pull.get("base"), "pull request base")
    head = _dict(pull.get("head"), "pull request head")
    head_repo = _dict(head.get("repo"), "pull request head repository")
    actual_number = _integer(pull.get("number"), "pull request number")
    if actual_number != number:
        raise VerificationError("GitHub returned a different pull request number")
    expected_api = f"{GITHUB_API}/repos/{REPOSITORY}/pulls/{number}"
    expected_html = f"{GITHUB_WEB}/{REPOSITORY}/pull/{number}"
    if pull.get("url") != expected_api or pull.get("html_url") != expected_html:
        raise VerificationError("pull request URLs do not bind the exact repository and number")
    result: dict[str, object] = {
        "number": number,
        "base_ref": _string(base.get("ref"), "pull request base ref"),
        "base_sha": _git_sha(base.get("sha"), "pull request base SHA"),
        "head_ref": _string(head.get("ref"), "pull request head ref"),
        "head_sha": _git_sha(head.get("sha"), "pull request head SHA"),
        "head_repository": _string(head_repo.get("full_name"), "head repository"),
    }
    if result["base_ref"] != "main":
        raise VerificationError("control pull request must target main")
    if not str(result["head_ref"]).startswith("agent/"):
        raise VerificationError("control pull request head must use agent/*")
    if result["head_repository"] != REPOSITORY:
        raise VerificationError("control pull request must not use a fork")
    if authority:
        return {
            "number": number,
            "api_url": expected_api,
            "html_url": expected_html,
            **{key: value for key, value in result.items() if key != "number"},
        }
    return result


def _comment_projection(
    payload: object,
    *,
    comment_id: int,
    pull_number: int,
    owner_id: int,
) -> tuple[dict[str, object], str, dict[str, object]]:
    comment = _dict(payload, "GitHub issue comment")
    actor = _dict(comment.get("user"), "GitHub comment actor")
    actual_id = _integer(comment.get("id"), "comment id")
    if actual_id != comment_id:
        raise VerificationError("GitHub returned a different comment id")
    api_url = f"{GITHUB_API}/repos/{REPOSITORY}/issues/comments/{comment_id}"
    issue_url = f"{GITHUB_API}/repos/{REPOSITORY}/issues/{pull_number}"
    html_url = f"{GITHUB_WEB}/{REPOSITORY}/pull/{pull_number}#issuecomment-{comment_id}"
    if comment.get("url") != api_url:
        raise VerificationError("comment API URL does not match its immutable id")
    if comment.get("issue_url") != issue_url:
        raise VerificationError("comment does not belong to the exact pull request")
    if comment.get("html_url") != html_url:
        raise VerificationError("comment HTML URL does not match repository, PR, and id")
    actor_id = _integer(actor.get("id"), "comment actor id")
    if actor.get("login") != OWNER_LOGIN or actor_id != owner_id:
        raise VerificationError("comment actor does not equal the live repository owner")
    if actor.get("type") != "User" or comment.get("author_association") != "OWNER":
        raise VerificationError("comment is not an OWNER-associated human user comment")
    created_at = _string(comment.get("created_at"), "comment created_at")
    updated_at = _string(comment.get("updated_at"), "comment updated_at")
    _parse_time(created_at, "comment created_at")
    _parse_time(updated_at, "comment updated_at")
    if created_at != updated_at:
        raise VerificationError("transaction comments must be posted once and remain unedited")
    body = _string(comment.get("body"), "comment body")
    try:
        parsed = json.loads(body)
    except json.JSONDecodeError as exc:
        raise VerificationError("comment body is not JSON") from exc
    parsed_object = _dict(parsed, "comment body")
    if body.encode("utf-8") != canonical_json_bytes(parsed_object):
        raise VerificationError("comment body is not exact canonical JSON")
    projection = {
        "id": comment_id,
        "node_id": _string(comment.get("node_id"), "comment node_id"),
        "api_url": api_url,
        "html_url": html_url,
        "issue_url": issue_url,
        "actor": {
            "login": OWNER_LOGIN,
            "id": actor_id,
            "type": "User",
            "author_association": "OWNER",
        },
        "created_at": created_at,
        "updated_at": updated_at,
        "body_utf8_sha256": _sha256(body.encode("utf-8")),
    }
    return projection, body, parsed_object


def _stage1_record(repository: GitRepository, base: str, head: str) -> dict[str, object]:
    base = _git_sha(base, "stage-1 base")
    head = _git_sha(head, "stage-1 head")
    commit = repository.commit(head)
    if commit["parents"] != [base]:
        raise VerificationError("stage-1 commit must be the single direct child of PR base")
    changed = repository.changed_files(base, head)
    if not changed:
        raise VerificationError("stage-1 control commit has no changed files")
    tree_listing = repository.full_tree_listing(head)
    patch = repository.deterministic_patch(base, head)
    return {
        "base_sha": base,
        "commit_sha": head,
        "parent_shas": [base],
        "tree_oid": commit["tree"],
        "full_tree_listing_sha256": _sha256(tree_listing),
        "deterministic_patch_sha256": _sha256(patch),
        "changed_files_manifest_sha256": evidence_sha256(changed),
        "changed_files": changed,
    }


def authorization_binding(stage1: Mapping[str, object]) -> dict[str, object]:
    return {
        "claim": AUTHORIZATION_CLAIM,
        "contract_sha256": CONTRACT_SHA256,
        "stage1_commit": stage1["commit_sha"],
        "stage1_tree": stage1["tree_oid"],
        "stage1_patch_sha256": stage1["deterministic_patch_sha256"],
        "changed_files_manifest_sha256": stage1["changed_files_manifest_sha256"],
        "receipt_path": RECEIPT_PATH,
        "temporary_nonrequired_check": TEMPORARY_NONREQUIRED_CHECK,
    }


def render_authorization_body(repository: GitRepository, base: str, stage1: str) -> str:
    return canonical_json_bytes(authorization_binding(_stage1_record(repository, base, stage1))).decode("utf-8")


def _validate_binding(value: object, *, completion: bool = False) -> dict[str, object]:
    binding = _dict(value, "comment binding")
    if completion:
        keys = {
            "claim", "transaction_id", "authority_evidence_sha256",
            "pre_merge_evidence_sha256", "pr_number", "base_sha", "head_sha",
            "final_patch_sha256", "merge_commit_sha", "merge_tree_oid",
            "protection_before_raw_sha256", "protection_during_raw_sha256",
            "protection_after_raw_sha256",
        }
    else:
        keys = {
            "claim", "contract_sha256", "stage1_commit", "stage1_tree",
            "stage1_patch_sha256", "changed_files_manifest_sha256",
            "receipt_path", "temporary_nonrequired_check",
        }
    _exact_keys(binding, keys, "comment binding")
    return binding


def build_authority_evidence(
    repository: GitRepository,
    github: GitHubReader,
    *,
    pull_number: int,
    comment_id: int,
    stage1_sha: str,
) -> dict[str, object]:
    repo_response = github.get_json(f"/repos/{REPOSITORY}")
    pull_response = github.get_json(f"/repos/{REPOSITORY}/pulls/{pull_number}")
    comment_response = github.get_json(
        f"/repos/{REPOSITORY}/issues/comments/{comment_id}"
    )
    repo = _repository_projection(repo_response.data)
    pull = _pull_projection(pull_response.data, pull_number, authority=True)
    if pull["head_sha"] != stage1_sha:
        raise VerificationError("authority comment must target the live stage-1 PR head")
    stage1 = _stage1_record(repository, str(pull["base_sha"]), stage1_sha)
    comment, _body, parsed = _comment_projection(
        comment_response.data,
        comment_id=comment_id,
        pull_number=pull_number,
        owner_id=int(repo["owner_id"]),
    )
    binding = _validate_binding(parsed)
    expected_binding = authorization_binding(stage1)
    if binding != expected_binding:
        raise VerificationError("authorization comment does not bind the exact stage-1 tree")
    evidence = {
        "schema_version": 1,
        "transaction_id": TRANSACTION_ID,
        "phase": "authority",
        "repository": repo,
        "pull_request": pull,
        "authorization_comment": comment,
        "stage1": stage1,
        "parsed_binding": binding,
        "raw_artifacts": {
            "repository": _artifact_ref(repo_response.raw),
            "pull_request": _artifact_ref(pull_response.raw),
            "authorization_comment": _artifact_ref(comment_response.raw),
        },
    }
    validate_authority_evidence(evidence)
    return evidence


def _validate_artifact_ref(value: object, label: str) -> None:
    reference = _dict(value, label)
    _exact_keys(reference, {"sha256", "byte_size"}, label)
    _sha256_value(reference.get("sha256"), f"{label}.sha256")
    size = reference.get("byte_size")
    if not isinstance(size, int) or isinstance(size, bool) or size < 0:
        raise VerificationError(f"{label}.byte_size must be a nonnegative integer")


def _validate_comment_shape(value: object, label: str) -> None:
    comment = _dict(value, label)
    _exact_keys(
        comment,
        {"id", "node_id", "api_url", "html_url", "issue_url", "actor", "created_at", "updated_at", "body_utf8_sha256"},
        label,
    )
    actor = _dict(comment.get("actor"), f"{label}.actor")
    _exact_keys(actor, {"login", "id", "type", "author_association"}, f"{label}.actor")


def _validate_changed_manifest(value: object, label: str) -> None:
    changed = _list(value, label)
    if not changed:
        raise VerificationError(f"{label} must not be empty")
    expected_keys = {"path", "status", "old_mode", "new_mode", "old_oid", "new_oid", "new_blob_sha256"}
    paths: list[str] = []
    for index, item in enumerate(changed):
        record = _dict(item, f"{label}[{index}]")
        _exact_keys(record, expected_keys, f"{label}[{index}]")
        paths.append(_safe_path(record.get("path"), f"{label}[{index}].path"))
        if record.get("status") not in {"A", "M"} or record.get("new_mode") != "100644":
            raise VerificationError(f"{label}[{index}] has a forbidden status or mode")
        if re.fullmatch(r"[0-7]{6}", str(record.get("old_mode"))) is None:
            raise VerificationError(f"{label}[{index}] old mode is malformed")
        _git_sha(record.get("old_oid"), f"{label}[{index}].old_oid")
        _git_sha(record.get("new_oid"), f"{label}[{index}].new_oid")
        _sha256_value(record.get("new_blob_sha256"), f"{label}[{index}].new_blob_sha256")
    if paths != sorted(paths) or len(paths) != len(set(paths)):
        raise VerificationError(f"{label} paths must be sorted and unique")


def _validate_repository_shape(value: object) -> None:
    repository = _dict(value, "evidence repository")
    _exact_keys(repository, {"full_name", "id", "owner_login", "owner_id"}, "evidence repository")
    if repository.get("full_name") != REPOSITORY or repository.get("owner_login") != OWNER_LOGIN:
        raise VerificationError("evidence repository identity is wrong")
    _integer(repository.get("id"), "evidence repository id")
    _integer(repository.get("owner_id"), "evidence owner id")


def validate_authority_evidence(evidence: object) -> None:
    value = _dict(evidence, "authority evidence")
    _exact_keys(
        value,
        {"schema_version", "transaction_id", "phase", "repository", "pull_request", "authorization_comment", "stage1", "parsed_binding", "raw_artifacts"},
        "authority evidence",
    )
    if value.get("schema_version") != 1 or value.get("transaction_id") != TRANSACTION_ID or value.get("phase") != "authority":
        raise VerificationError("authority evidence identity is wrong")
    _validate_repository_shape(value.get("repository"))
    pull = _dict(value.get("pull_request"), "authority pull request")
    _exact_keys(pull, {"number", "api_url", "html_url", "base_ref", "base_sha", "head_ref", "head_sha", "head_repository"}, "authority pull request")
    _validate_comment_shape(value.get("authorization_comment"), "authorization comment")
    stage1 = _dict(value.get("stage1"), "stage1")
    _exact_keys(stage1, {"base_sha", "commit_sha", "parent_shas", "tree_oid", "full_tree_listing_sha256", "deterministic_patch_sha256", "changed_files_manifest_sha256", "changed_files"}, "stage1")
    _validate_changed_manifest(stage1.get("changed_files"), "stage1.changed_files")
    binding = _validate_binding(value.get("parsed_binding"))
    if binding != authorization_binding(stage1):
        raise VerificationError("authority parsed binding differs from stage1")
    comment = _dict(value.get("authorization_comment"), "authorization comment")
    if _sha256(canonical_json_bytes(binding)) != comment.get("body_utf8_sha256"):
        raise VerificationError(
            "authorization comment body hash does not match parsed binding"
        )
    raw = _dict(value.get("raw_artifacts"), "authority raw_artifacts")
    _exact_keys(raw, {"repository", "pull_request", "authorization_comment"}, "authority raw_artifacts")
    for key in sorted(raw):
        _validate_artifact_ref(raw[key], f"authority raw_artifacts.{key}")


def _normalize_protection(
    raw: Mapping[str, object],
    *,
    observed_at: str,
) -> dict[str, object]:
    _exact_keys(
        raw,
        {
            "branch",
            "protection",
            "repository",
            "rulesets",
            "raw_response_sha256",
        },
        "raw protection capture",
    )
    branch = _dict(raw["branch"], "main branch response")
    protection = _dict(raw["protection"], "branch protection response")
    repository = _dict(raw["repository"], "repository settings response")
    rulesets = _list(raw["rulesets"], "repository ruleset inventory")
    raw_response_sha256 = _dict(
        raw["raw_response_sha256"], "raw protection response hashes"
    )
    _exact_keys(
        raw_response_sha256,
        {"branch", "protection", "repository", "rulesets"},
        "raw protection response hashes",
    )
    for key, value in raw_response_sha256.items():
        _sha256_value(value, f"raw protection response hashes.{key}")
    commit = _dict(branch.get("commit"), "main branch commit")
    required = _dict(protection.get("required_status_checks"), "required status checks")
    checks: list[dict[str, object]] = []
    for index, item in enumerate(_list(required.get("checks"), "required checks")):
        check = _dict(item, f"required checks[{index}]")
        checks.append(
            {
                "context": _string(check.get("context"), "required check context"),
                "app_id": _integer(check.get("app_id"), "required check app id"),
            }
        )
    checks.sort(key=lambda item: str(item["context"]))
    reviews = _dict(protection.get("required_pull_request_reviews"), "required pull request reviews")
    if (
        reviews.get("dismiss_stale_reviews") is not False
        or reviews.get("require_code_owner_reviews") is not False
        or reviews.get("required_approving_review_count") != 0
        or reviews.get("require_last_push_approval") is not False
    ):
        raise VerificationError(
            "pull request review protection differs from the exact retained state"
        )
    def actors_empty(value: object, label: str) -> bool:
        # GitHub serializes an empty classic-protection actor set as either
        # null/omitted or an explicit users/teams/apps map. Both are the same
        # exact empty state; any populated entry remains a hard failure.
        if value is None:
            return True
        actors = _dict(value, label)
        return all(
            _list(actors.get(key, []), f"{label} {key}") == []
            for key in ("users", "teams", "apps")
        )

    bypass_empty = actors_empty(
        reviews.get("bypass_pull_request_allowances"),
        "pull request bypass allowances",
    )
    restrictions_empty = actors_empty(
        protection.get("restrictions"),
        "push restrictions",
    )
    merge_methods = (
        repository.get("allow_squash_merge") is True
        and repository.get("allow_merge_commit") is False
        and repository.get("allow_rebase_merge") is False
    )
    return {
        "observed_at": observed_at,
        "main_sha": _git_sha(commit.get("sha"), "main branch SHA"),
        "strict": required.get("strict") is True,
        "required_checks": checks,
        "enforce_admins": _dict(protection.get("enforce_admins"), "enforce_admins").get("enabled") is True,
        "pull_request_required": True,
        "dismiss_stale_reviews": False,
        "conversation_resolution": _dict(protection.get("required_conversation_resolution"), "conversation resolution").get("enabled") is True,
        "linear_history": _dict(protection.get("required_linear_history"), "linear history").get("enabled") is True,
        "bypass_allowances_empty": bypass_empty,
        "rulesets_empty": rulesets == [],
        "push_restrictions_empty": restrictions_empty,
        "force_push_disabled": _dict(protection.get("allow_force_pushes"), "allow_force_pushes").get("enabled") is False,
        "deletion_disabled": _dict(protection.get("allow_deletions"), "allow_deletions").get("enabled") is False,
        "squash_only": merge_methods,
        "raw_response_sha256": dict(raw_response_sha256),
        "raw_sha256": _sha256(canonical_json_bytes(raw)),
    }


def capture_protection(
    github: GitHubReader,
    *,
    observed_at: datetime | None = None,
    protection_github: GitHubReader | None = None,
) -> dict[str, object]:
    protected_reader = protection_github or github
    branch = github.get_json(f"/repos/{REPOSITORY}/branches/main")
    protection = protected_reader.get_json(
        f"/repos/{REPOSITORY}/branches/main/protection"
    )
    repository = github.get_json(f"/repos/{REPOSITORY}")
    rulesets = protected_reader.get_json(
        f"/repos/{REPOSITORY}/rulesets?includes_parents=true"
    )
    raw = {
        "branch": branch.data,
        "protection": protection.data,
        "repository": repository.data,
        "rulesets": rulesets.data,
        "raw_response_sha256": {
            "branch": _sha256(branch.raw),
            "protection": _sha256(protection.raw),
            "repository": _sha256(repository.raw),
            "rulesets": _sha256(rulesets.raw),
        },
    }
    capture = {
        "schema_version": 1,
        "kind": "wp0002-local-operator-protection-capture",
        "normalized": _normalize_protection(raw, observed_at=_timestamp(observed_at)),
        "raw": raw,
    }
    _validate_protection_capture(capture)
    return capture


def _validate_protection_shape(value: object, label: str) -> None:
    protection = _dict(value, label)
    _exact_keys(protection, {"observed_at", "main_sha", "strict", "required_checks", "enforce_admins", "pull_request_required", "dismiss_stale_reviews", "conversation_resolution", "linear_history", "bypass_allowances_empty", "rulesets_empty", "push_restrictions_empty", "force_push_disabled", "deletion_disabled", "squash_only", "raw_response_sha256", "raw_sha256"}, label)
    _parse_time(protection.get("observed_at"), f"{label}.observed_at")
    _git_sha(protection.get("main_sha"), f"{label}.main_sha")
    _sha256_value(protection.get("raw_sha256"), f"{label}.raw_sha256")
    response_hashes = _dict(
        protection.get("raw_response_sha256"),
        f"{label}.raw_response_sha256",
    )
    _exact_keys(
        response_hashes,
        {"branch", "protection", "repository", "rulesets"},
        f"{label}.raw_response_sha256",
    )
    for key, value in response_hashes.items():
        _sha256_value(value, f"{label}.raw_response_sha256.{key}")
    checks = _list(protection.get("required_checks"), f"{label}.required_checks")
    for index, item in enumerate(checks):
        check = _dict(item, f"{label}.required_checks[{index}]")
        _exact_keys(check, {"context", "app_id"}, f"{label}.required_checks[{index}]")


def _validate_protection_capture(value: object) -> tuple[dict[str, object], bytes]:
    capture = _dict(value, "protection capture")
    _exact_keys(capture, {"schema_version", "kind", "normalized", "raw"}, "protection capture")
    if capture.get("schema_version") != 1 or capture.get("kind") != "wp0002-local-operator-protection-capture":
        raise VerificationError("protection capture identity is wrong")
    normalized = _dict(capture.get("normalized"), "normalized protection")
    _validate_protection_shape(normalized, "normalized protection")
    raw = _dict(capture.get("raw"), "raw protection")
    recomputed = _normalize_protection(raw, observed_at=str(normalized["observed_at"]))
    if recomputed != normalized:
        raise VerificationError("normalized protection does not match its raw capture")
    return normalized, canonical_json_bytes(raw)


def _protection_state_without_phase_fields(value: Mapping[str, object]) -> dict[str, object]:
    return {
        key: item
        for key, item in value.items()
        if key not in {
            "observed_at",
            "main_sha",
            "required_checks",
            "raw_response_sha256",
            "raw_sha256",
        }
    }


def _required_check_set(value: Mapping[str, object]) -> set[tuple[str, int]]:
    return {
        (str(item["context"]), int(item["app_id"]))
        for item in _list(value.get("required_checks"), "required checks")
        if isinstance(item, dict)
    }


def _require_protection_contract(
    before: Mapping[str, object],
    during: Mapping[str, object],
    *,
    base_sha: str,
) -> None:
    for label, protection in (("before", before), ("during", during)):
        _validate_protection_shape(protection, f"protection_{label}")
        if protection.get("main_sha") != base_sha:
            raise VerificationError(f"protection {label} main SHA differs from PR base")
        required_true = (
            "strict", "enforce_admins", "pull_request_required",
            "conversation_resolution", "linear_history", "bypass_allowances_empty",
            "rulesets_empty", "push_restrictions_empty", "force_push_disabled", "deletion_disabled",
            "squash_only",
        )
        if any(protection.get(key) is not True for key in required_true):
            raise VerificationError(f"protection {label} weakens a retained invariant")
        if protection.get("dismiss_stale_reviews") is not False:
            raise VerificationError(f"protection {label} enables stale-review dismissal")
    expected_before = {(name, REQUIRED_APP_ID) for name in FULL_REQUIRED_CHECKS}
    expected_during = {(name, REQUIRED_APP_ID) for name in REQUIRED_CHECKS}
    if (
        _required_check_set(before) != expected_before
        or len(_list(before.get("required_checks"), "before required checks")) != 3
    ):
        raise VerificationError("before protection must require exactly all three checks")
    if (
        _required_check_set(during) != expected_during
        or len(_list(during.get("required_checks"), "during required checks")) != 2
    ):
        raise VerificationError("during protection must remove only wp0002-policy")
    if _protection_state_without_phase_fields(before) != _protection_state_without_phase_fields(during):
        raise VerificationError("during protection changes a non-temporary setting")
    if _parse_time(before["observed_at"], "before observed_at") > _parse_time(during["observed_at"], "during observed_at"):
        raise VerificationError("protection captures are not chronological")


RECEIPT_KEYS = {
    "receipt_id", "issued_at", "issued_by", "issuer_role", "receipt_kind",
    "artifact_resolver", "source_reference", "subject_ids", "subject_claims",
    "approval_text_sha256", "accepted_commit", "artifact_sha256",
    "subject_contract_sha256", "subject_event_sha256", "foundation_binding",
    "signature_reference", "sealed",
}


def _validate_receipt(
    receipt: object,
    *,
    authority: Mapping[str, object],
    expected_artifacts: Mapping[str, str],
) -> None:
    value = _dict(receipt, "local operator receipt")
    _exact_keys(value, RECEIPT_KEYS, "local operator receipt")
    comment = _dict(authority["authorization_comment"], "authority comment")
    source = str(comment["html_url"])
    resolver = _dict(value.get("artifact_resolver"), "receipt artifact_resolver")
    _exact_keys(resolver, {"type", "resolver_reference"}, "receipt artifact_resolver")
    claims = [
        {"subject_id": "WP-0002", "claims": [AUTHORIZATION_CLAIM]}
    ]
    exact = {
        "receipt_id": RECEIPT_ID,
        "issued_by": OWNER_LOGIN,
        "issuer_role": "creator",
        "receipt_kind": "creator-authorization",
        "source_reference": source,
        "subject_ids": ["WP-0002"],
        "subject_claims": claims,
        "approval_text_sha256": comment["body_utf8_sha256"],
        "accepted_commit": _dict(authority["stage1"], "stage1")["commit_sha"],
        "subject_contract_sha256": {"WP-0002": CONTRACT_SHA256},
        "subject_event_sha256": {},
        "foundation_binding": None,
        "signature_reference": source,
        "sealed": True,
    }
    for key, expected in exact.items():
        if value.get(key) != expected:
            raise VerificationError(f"local operator receipt has wrong {key}")
    _parse_time(value.get("issued_at"), "receipt issued_at")
    if resolver != {"type": "external-protected", "resolver_reference": source}:
        raise VerificationError("receipt resolver does not bind the authority comment")
    artifacts = _dict(value.get("artifact_sha256"), "receipt artifact_sha256")
    if artifacts != dict(expected_artifacts):
        missing = sorted(set(expected_artifacts) - set(artifacts))
        extra = sorted(set(artifacts) - set(expected_artifacts))
        raise VerificationError(
            f"receipt artifact keys or values differ; missing={missing}, extra={extra}"
        )


def _check_runs(payload: object, head_sha: str) -> list[dict[str, object]]:
    document = _dict(payload, "check-runs response")
    runs = _list(document.get("check_runs"), "check-runs response.check_runs")
    selected: list[dict[str, object]] = []
    for name in REQUIRED_CHECKS:
        candidates: list[dict[str, Any]] = []
        for item in runs:
            run = _dict(item, "check run")
            if run.get("name") == name and run.get("head_sha") == head_sha:
                candidates.append(run)
        if not candidates:
            raise VerificationError(f"final head lacks check run {name}")
        candidates.sort(key=lambda run: int(run.get("id", 0)))
        latest = candidates[-1]
        app = _dict(latest.get("app"), f"{name} check app")
        if app.get("id") != REQUIRED_APP_ID:
            raise VerificationError(f"latest {name} run is not from app {REQUIRED_APP_ID}")
        if latest.get("status") != "completed" or latest.get("conclusion") != "success":
            raise VerificationError(f"latest {name} run is not successful")
        selected.append(
            {
                "name": name,
                "app_id": REQUIRED_APP_ID,
                "head_sha": head_sha,
                "status": "completed",
                "conclusion": "success",
            }
        )
    selected.sort(key=lambda item: str(item["name"]))
    return selected


def _live_comment_again(
    github: GitHubReader,
    authority: Mapping[str, object],
) -> tuple[APIResult, dict[str, object], dict[str, object]]:
    comment = _dict(authority["authorization_comment"], "authority comment")
    pull = _dict(authority["pull_request"], "authority pull request")
    result = github.get_json(
        f"/repos/{REPOSITORY}/issues/comments/{int(comment['id'])}"
    )
    projection, _body, parsed = _comment_projection(
        result.data,
        comment_id=int(comment["id"]),
        pull_number=int(pull["number"]),
        owner_id=int(_dict(authority["repository"], "authority repository")["owner_id"]),
    )
    if projection != comment or parsed != authority["parsed_binding"]:
        raise VerificationError("authorization comment changed after authority capture")
    return result, projection, parsed


def _live_authorization_comment_from_pre_merge(
    github: GitHubReader,
    pre_merge: Mapping[str, object],
) -> APIResult:
    """Re-authenticate the immutable owner authorization at completion time."""

    expected = _dict(
        pre_merge["authorization_comment"], "pre-merge authorization comment"
    )
    pull = _dict(pre_merge["final_pull_request"], "pre-merge final pull request")
    repository = _dict(pre_merge["repository"], "pre-merge repository")
    response = github.get_json(
        f"/repos/{REPOSITORY}/issues/comments/{int(expected['id'])}"
    )
    projection, _body, parsed = _comment_projection(
        response.data,
        comment_id=int(expected["id"]),
        pull_number=int(pull["number"]),
        owner_id=int(repository["owner_id"]),
    )
    binding = _validate_binding(parsed)
    receipt = _dict(
        pre_merge["receipt_materialization"], "receipt materialization"
    )
    if (
        projection != expected
        or binding.get("claim") != AUTHORIZATION_CLAIM
        or binding.get("contract_sha256") != CONTRACT_SHA256
        or binding.get("stage1_commit") != receipt.get("parent_sha")
        or binding.get("receipt_path") != RECEIPT_PATH
        or binding.get("temporary_nonrequired_check")
        != TEMPORARY_NONREQUIRED_CHECK
    ):
        raise VerificationError(
            "authorization comment changed after pre-merge capture"
        )
    return response


def _same_live_protection(
    github: GitHubReader,
    expected: Mapping[str, object],
    *,
    now: datetime | None = None,
) -> None:
    live = capture_protection(github, observed_at=now)
    normalized, _ = _validate_protection_capture(live)
    left = {key: value for key, value in normalized.items() if key not in {"observed_at", "raw_sha256"}}
    right = {key: value for key, value in expected.items() if key not in {"observed_at", "raw_sha256"}}
    if left != right:
        raise VerificationError("live branch protection drifted from the supplied capture")


def build_pre_merge_evidence(
    repository: GitRepository,
    github: GitHubReader,
    authority: Mapping[str, object],
    protection_before_capture: Mapping[str, object],
    protection_during_capture: Mapping[str, object],
    *,
    verify_live_during: bool = True,
    now: datetime | None = None,
) -> dict[str, object]:
    validate_authority_evidence(authority)
    before, before_raw = _validate_protection_capture(protection_before_capture)
    during, during_raw = _validate_protection_capture(protection_during_capture)
    repo_response = github.get_json(f"/repos/{REPOSITORY}")
    repo = _repository_projection(repo_response.data)
    if repo != authority["repository"]:
        raise VerificationError("repository identity changed after authority capture")
    pull_number = int(_dict(authority["pull_request"], "authority pull")["number"])
    pull_response = github.get_json(f"/repos/{REPOSITORY}/pulls/{pull_number}")
    pull = _pull_projection(pull_response.data, pull_number, authority=False)
    if _dict(pull_response.data, "final pull request").get("auto_merge") is not None:
        raise VerificationError("control pull request must not use GitHub auto-merge")
    authority_pull = _dict(authority["pull_request"], "authority pull request")
    if pull["base_sha"] != authority_pull["base_sha"] or pull["base_ref"] != authority_pull["base_ref"] or pull["head_ref"] != authority_pull["head_ref"]:
        raise VerificationError("final pull request changed its authorized base or branch")
    _live_comment_again(github, authority)
    stage1 = _dict(authority["stage1"], "stage1")
    final_head = str(pull["head_sha"])
    final_commit = repository.commit(final_head)
    if final_commit["parents"] != [stage1["commit_sha"]]:
        raise VerificationError("final head must be one direct receipt commit after stage1")
    delta = repository.changed_files(str(stage1["commit_sha"]), final_head)
    if len(delta) != 1 or delta[0]["path"] != RECEIPT_PATH or delta[0]["status"] != "A":
        raise VerificationError("stage1-to-final delta must add only the exact receipt file")
    receipt_bytes = repository.blob_at(final_head, RECEIPT_PATH)
    try:
        receipt = json.loads(receipt_bytes.decode("utf-8"))
    except (UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise VerificationError("receipt blob is not UTF-8 JSON") from exc
    source = str(_dict(authority["authorization_comment"], "authority comment")["html_url"])
    external_key = source.removeprefix("https://")
    expected_artifacts = {
        external_key: str(_dict(authority["authorization_comment"], "authority comment")["body_utf8_sha256"])
    }
    for item in _list(stage1["changed_files"], "stage1 changed files"):
        changed = _dict(item, "stage1 changed file")
        expected_artifacts[str(changed["path"])] = str(changed["new_blob_sha256"])
    _validate_receipt(receipt, authority=authority, expected_artifacts=expected_artifacts)
    final_changed = repository.changed_files(str(pull["base_sha"]), final_head)
    final_patch = repository.deterministic_patch(str(pull["base_sha"]), final_head)
    patch_response = github.get_bytes(
        f"/repos/{REPOSITORY}/pulls/{pull_number}",
        accept="application/vnd.github.patch",
    )
    checks_response = github.get_json(
        f"/repos/{REPOSITORY}/commits/{final_head}/check-runs?per_page=100"
    )
    checks = _check_runs(checks_response.data, final_head)
    _require_protection_contract(before, during, base_sha=str(pull["base_sha"]))
    if verify_live_during:
        _same_live_protection(github, during, now=now)
    evidence = {
        "schema_version": 1,
        "transaction_id": TRANSACTION_ID,
        "phase": "pre-merge",
        "repository": repo,
        "authority_evidence_sha256": evidence_sha256(authority),
        "authorization_comment": authority["authorization_comment"],
        "final_pull_request": {
            **pull,
            "deterministic_patch_sha256": _sha256(final_patch),
            "github_patch_sha256": _sha256(patch_response.raw),
            "changed_files_manifest_sha256": evidence_sha256(final_changed),
        },
        "receipt_materialization": {
            "commit_sha": final_head,
            "parent_sha": stage1["commit_sha"],
            "tree_oid": final_commit["tree"],
            "receipt_path": RECEIPT_PATH,
            "receipt_sha256": _sha256(receipt_bytes),
            "delta_patch_sha256": _sha256(repository.deterministic_patch(str(stage1["commit_sha"]), final_head)),
            "delta": delta,
        },
        "required_check_runs": checks,
        "protection_before": before,
        "protection_during": during,
        "raw_artifacts": {
            "pull_request": _artifact_ref(pull_response.raw),
            "github_patch": _artifact_ref(patch_response.raw),
            "check_runs": _artifact_ref(checks_response.raw),
            "protection_before": _artifact_ref(before_raw),
            "protection_during": _artifact_ref(during_raw),
        },
    }
    validate_pre_merge_evidence(evidence)
    return evidence


def validate_pre_merge_evidence(evidence: object) -> None:
    value = _dict(evidence, "pre-merge evidence")
    _exact_keys(value, {"schema_version", "transaction_id", "phase", "repository", "authority_evidence_sha256", "authorization_comment", "final_pull_request", "receipt_materialization", "required_check_runs", "protection_before", "protection_during", "raw_artifacts"}, "pre-merge evidence")
    if value.get("schema_version") != 1 or value.get("transaction_id") != TRANSACTION_ID or value.get("phase") != "pre-merge":
        raise VerificationError("pre-merge evidence identity is wrong")
    _validate_repository_shape(value.get("repository"))
    _sha256_value(value.get("authority_evidence_sha256"), "authority evidence hash")
    _validate_comment_shape(value.get("authorization_comment"), "pre-merge authorization comment")
    pull = _dict(value.get("final_pull_request"), "final pull request")
    _exact_keys(pull, {"number", "base_ref", "base_sha", "head_ref", "head_sha", "head_repository", "deterministic_patch_sha256", "github_patch_sha256", "changed_files_manifest_sha256"}, "final pull request")
    receipt = _dict(value.get("receipt_materialization"), "receipt materialization")
    _exact_keys(receipt, {"commit_sha", "parent_sha", "tree_oid", "receipt_path", "receipt_sha256", "delta_patch_sha256", "delta"}, "receipt materialization")
    _validate_changed_manifest(receipt.get("delta"), "receipt delta")
    if len(receipt["delta"]) != 1 or receipt["receipt_path"] != RECEIPT_PATH:
        raise VerificationError("receipt materialization is not exact")
    runs = _list(value.get("required_check_runs"), "required check runs")
    if {item.get("name") for item in runs if isinstance(item, dict)} != set(REQUIRED_CHECKS) or len(runs) != 2:
        raise VerificationError("pre-merge evidence lacks the exact two required checks")
    for index, item in enumerate(runs):
        run = _dict(item, f"required check runs[{index}]")
        _exact_keys(
            run,
            {"name", "app_id", "head_sha", "status", "conclusion"},
            f"required check runs[{index}]",
        )
        if (
            run.get("name") not in REQUIRED_CHECKS
            or run.get("app_id") != REQUIRED_APP_ID
            or run.get("head_sha") != pull.get("head_sha")
            or run.get("status") != "completed"
            or run.get("conclusion") != "success"
        ):
            raise VerificationError(
                f"required check runs[{index}] does not bind a successful final-head run"
            )
    before = _dict(value.get("protection_before"), "protection before")
    during = _dict(value.get("protection_during"), "protection during")
    _require_protection_contract(before, during, base_sha=str(pull["base_sha"]))
    raw = _dict(value.get("raw_artifacts"), "pre-merge raw_artifacts")
    _exact_keys(raw, {"pull_request", "github_patch", "check_runs", "protection_before", "protection_during"}, "pre-merge raw_artifacts")
    for key in sorted(raw):
        _validate_artifact_ref(raw[key], f"pre-merge raw_artifacts.{key}")


def completion_binding(
    pre_merge: Mapping[str, object],
    *,
    authority_evidence_sha256: str,
    merge_commit_sha: str,
    merge_tree_oid: str,
    protection_after: Mapping[str, object],
) -> dict[str, object]:
    pull = _dict(pre_merge["final_pull_request"], "final pull request")
    before = _dict(pre_merge["protection_before"], "protection before")
    during = _dict(pre_merge["protection_during"], "protection during")
    return {
        "claim": COMPLETION_CLAIM,
        "transaction_id": TRANSACTION_ID,
        "authority_evidence_sha256": authority_evidence_sha256,
        "pre_merge_evidence_sha256": evidence_sha256(pre_merge),
        "pr_number": pull["number"],
        "base_sha": pull["base_sha"],
        "head_sha": pull["head_sha"],
        "final_patch_sha256": pull["deterministic_patch_sha256"],
        "merge_commit_sha": merge_commit_sha,
        "merge_tree_oid": merge_tree_oid,
        "protection_before_raw_sha256": before["raw_sha256"],
        "protection_during_raw_sha256": during["raw_sha256"],
        "protection_after_raw_sha256": protection_after["raw_sha256"],
    }


def _merged_state(
    repository: GitRepository,
    github: GitHubReader,
    pre_merge: Mapping[str, object],
    protection_after_capture: Mapping[str, object],
    *,
    verify_live_after: bool,
    now: datetime | None,
) -> tuple[dict[str, object], dict[str, object], dict[str, object], bytes, APIResult, APIResult]:
    validate_pre_merge_evidence(pre_merge)
    after, after_raw = _validate_protection_capture(protection_after_capture)
    pull = _dict(pre_merge["final_pull_request"], "pre-merge final pull request")
    number = int(pull["number"])
    pull_response = github.get_json(f"/repos/{REPOSITORY}/pulls/{number}")
    payload = _dict(pull_response.data, "merged pull request")
    live_pull = _pull_projection(payload, number, authority=False)
    if any(live_pull[key] != pull[key] for key in ("base_sha", "head_sha", "base_ref", "head_ref", "head_repository")):
        raise VerificationError("merged pull request differs from pre-merge evidence")
    if payload.get("merged") is not True:
        raise VerificationError("pull request is not merged")
    merged_at = _string(payload.get("merged_at"), "pull request merged_at")
    merge_sha = _git_sha(payload.get("merge_commit_sha"), "merge commit SHA")
    merge_commit = repository.commit(merge_sha)
    if merge_commit["parents"] != [pull["base_sha"]]:
        raise VerificationError("squash commit does not have the exact PR base as sole parent")
    final_commit = repository.commit(str(pull["head_sha"]))
    if merge_commit["tree"] != final_commit["tree"]:
        raise VerificationError("squash commit tree differs from final PR head tree")
    main_response = github.get_json(f"/repos/{REPOSITORY}/branches/main")
    main = _dict(main_response.data, "main branch")
    main_commit = _dict(main.get("commit"), "main branch commit")
    main_sha = _git_sha(main_commit.get("sha"), "main branch SHA")
    if main_sha != merge_sha or not repository.contains(merge_sha, main_sha):
        raise VerificationError("main does not point to the verified squash commit")
    before = _dict(pre_merge["protection_before"], "protection before")
    if _required_check_set(after) != {(name, REQUIRED_APP_ID) for name in FULL_REQUIRED_CHECKS}:
        raise VerificationError("after protection does not restore all three checks")
    if _protection_state_without_phase_fields(after) != _protection_state_without_phase_fields(before):
        raise VerificationError("after protection differs from the retained before state")
    if after["main_sha"] != merge_sha:
        raise VerificationError("after protection capture does not target merged main")
    restore_delay = (
        _parse_time(after["observed_at"], "after observed_at")
        - _parse_time(merged_at, "merged_at")
    ).total_seconds()
    if restore_delay < 0:
        raise VerificationError("after protection capture predates the merge")
    if restore_delay > MAX_RESTORE_DELAY_SECONDS:
        raise VerificationError(
            "after protection capture exceeds the 600-second restoration window"
        )
    if verify_live_after:
        _same_live_protection(github, after, now=now)
    return payload, live_pull, merge_commit, after_raw, pull_response, main_response


def render_completion_body(
    repository: GitRepository,
    github: GitHubReader,
    pre_merge: Mapping[str, object],
    protection_after_capture: Mapping[str, object],
    *,
    verify_live_after: bool = True,
    now: datetime | None = None,
) -> str:
    _payload, _pull, merge, _after_raw, _pull_response, _main_response = _merged_state(
        repository,
        github,
        pre_merge,
        protection_after_capture,
        verify_live_after=verify_live_after,
        now=now,
    )
    after, _ = _validate_protection_capture(protection_after_capture)
    binding = completion_binding(
        pre_merge,
        authority_evidence_sha256=str(pre_merge["authority_evidence_sha256"]),
        merge_commit_sha=str(merge["sha"]),
        merge_tree_oid=str(merge["tree"]),
        protection_after=after,
    )
    return canonical_json_bytes(binding).decode("utf-8")


def build_complete_evidence(
    repository: GitRepository,
    github: GitHubReader,
    pre_merge: Mapping[str, object],
    protection_after_capture: Mapping[str, object],
    *,
    completion_comment_id: int,
    verify_live_after: bool = True,
    now: datetime | None = None,
) -> dict[str, object]:
    payload, live_pull, merge, after_raw, pull_response, main_response = _merged_state(
        repository,
        github,
        pre_merge,
        protection_after_capture,
        verify_live_after=verify_live_after,
        now=now,
    )
    after, _ = _validate_protection_capture(protection_after_capture)
    repo_response = github.get_json(f"/repos/{REPOSITORY}")
    repo = _repository_projection(repo_response.data)
    if repo != pre_merge["repository"]:
        raise VerificationError("repository identity changed before completion")
    authorization_comment_response = _live_authorization_comment_from_pre_merge(
        github, pre_merge
    )
    number = int(live_pull["number"])
    comment_response = github.get_json(
        f"/repos/{REPOSITORY}/issues/comments/{completion_comment_id}"
    )
    comment, _body, parsed = _comment_projection(
        comment_response.data,
        comment_id=completion_comment_id,
        pull_number=number,
        owner_id=int(repo["owner_id"]),
    )
    binding = _validate_binding(parsed, completion=True)
    expected = completion_binding(
        pre_merge,
        authority_evidence_sha256=str(pre_merge["authority_evidence_sha256"]),
        merge_commit_sha=str(merge["sha"]),
        merge_tree_oid=str(merge["tree"]),
        protection_after=after,
    )
    if binding != expected:
        raise VerificationError("completion comment does not seal the exact transaction")
    if _parse_time(comment["created_at"], "completion comment created_at") < _parse_time(after["observed_at"], "after observed_at"):
        raise VerificationError("completion comment predates restored protection")
    evidence = {
        "schema_version": 1,
        "transaction_id": TRANSACTION_ID,
        "phase": "complete",
        "repository": repo,
        "pre_merge_evidence_sha256": evidence_sha256(pre_merge),
        "merged_pull_request": {
            "number": number,
            "merged": True,
            "merged_at": payload["merged_at"],
            "merge_commit_sha": merge["sha"],
            "base_sha": live_pull["base_sha"],
            "head_sha": live_pull["head_sha"],
        },
        "merge": {
            "sole_parent_sha": merge["parents"][0],
            "tree_oid": merge["tree"],
            "final_head_tree_oid": merge["tree"],
            "tree_matches_final_head": True,
            "main_contains_merge": True,
        },
        "protection_after": after,
        "completion_comment": comment,
        "parsed_completion_binding": binding,
        "next_canary_required": "first-post-restoration-implementation-pr",
        "raw_artifacts": {
            "pull_request": _artifact_ref(pull_response.raw),
            "main": _artifact_ref(main_response.raw),
            "protection_after": _artifact_ref(after_raw),
            "authorization_comment": _artifact_ref(
                authorization_comment_response.raw
            ),
            "completion_comment": _artifact_ref(comment_response.raw),
        },
    }
    validate_complete_evidence(evidence)
    return evidence


def validate_complete_evidence(evidence: object) -> None:
    value = _dict(evidence, "complete evidence")
    _exact_keys(value, {"schema_version", "transaction_id", "phase", "repository", "pre_merge_evidence_sha256", "merged_pull_request", "merge", "protection_after", "completion_comment", "parsed_completion_binding", "next_canary_required", "raw_artifacts"}, "complete evidence")
    if value.get("schema_version") != 1 or value.get("transaction_id") != TRANSACTION_ID or value.get("phase") != "complete":
        raise VerificationError("complete evidence identity is wrong")
    _validate_repository_shape(value.get("repository"))
    _sha256_value(value.get("pre_merge_evidence_sha256"), "pre-merge evidence hash")
    merged = _dict(value.get("merged_pull_request"), "merged pull request")
    _exact_keys(merged, {"number", "merged", "merged_at", "merge_commit_sha", "base_sha", "head_sha"}, "merged pull request")
    merge = _dict(value.get("merge"), "merge proof")
    _exact_keys(merge, {"sole_parent_sha", "tree_oid", "final_head_tree_oid", "tree_matches_final_head", "main_contains_merge"}, "merge proof")
    if merged.get("merged") is not True or merge.get("tree_matches_final_head") is not True or merge.get("main_contains_merge") is not True:
        raise VerificationError("complete evidence lacks an exact successful merge")
    _validate_protection_shape(value.get("protection_after"), "protection_after")
    _validate_comment_shape(value.get("completion_comment"), "completion_comment")
    binding = _validate_binding(value.get("parsed_completion_binding"), completion=True)
    if binding.get("claim") != COMPLETION_CLAIM or binding.get("transaction_id") != TRANSACTION_ID:
        raise VerificationError("parsed completion binding has the wrong authority claim")
    for key in (
        "authority_evidence_sha256",
        "pre_merge_evidence_sha256",
        "final_patch_sha256",
        "protection_before_raw_sha256",
        "protection_during_raw_sha256",
        "protection_after_raw_sha256",
    ):
        _sha256_value(binding.get(key), f"parsed completion binding.{key}")
    for key in ("base_sha", "head_sha", "merge_commit_sha", "merge_tree_oid"):
        _git_sha(binding.get(key), f"parsed completion binding.{key}")
    _integer(binding.get("pr_number"), "parsed completion binding.pr_number")
    expected_cross_bindings = {
        "pre_merge_evidence_sha256": value.get("pre_merge_evidence_sha256"),
        "pr_number": merged.get("number"),
        "base_sha": merged.get("base_sha"),
        "head_sha": merged.get("head_sha"),
        "merge_commit_sha": merged.get("merge_commit_sha"),
        "merge_tree_oid": merge.get("tree_oid"),
        "protection_after_raw_sha256": _dict(
            value.get("protection_after"), "protection_after"
        ).get("raw_sha256"),
    }
    for key, expected in expected_cross_bindings.items():
        if binding.get(key) != expected:
            raise VerificationError(
                f"parsed completion binding does not match complete evidence at {key}"
            )
    comment = _dict(value.get("completion_comment"), "completion_comment")
    if _sha256(canonical_json_bytes(binding)) != comment.get("body_utf8_sha256"):
        raise VerificationError(
            "completion comment body hash does not match parsed completion binding"
        )
    if value.get("next_canary_required") != "first-post-restoration-implementation-pr":
        raise VerificationError("complete evidence suppresses the post-restoration canary")
    raw = _dict(value.get("raw_artifacts"), "complete raw_artifacts")
    _exact_keys(raw, {"pull_request", "main", "protection_after", "authorization_comment", "completion_comment"}, "complete raw_artifacts")
    for key in sorted(raw):
        _validate_artifact_ref(raw[key], f"complete raw_artifacts.{key}")


def _json_blob_at(
    repository: GitRepository,
    commit: str,
    path: str,
) -> tuple[dict[str, Any], bytes]:
    raw = repository.blob_at(commit, path)
    try:
        value = json.loads(raw.decode("utf-8"))
    except (UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise VerificationError(f"closure report {path} is not UTF-8 JSON") from exc
    return _dict(value, f"closure report {path}"), raw


def _validate_closure_report_chain(
    repository: GitRepository,
    authority: Mapping[str, object],
    pre_merge: Mapping[str, object],
    complete: Mapping[str, object],
    *,
    closure_base: str,
) -> dict[str, object]:
    """Recompute the report chain from Git objects, not candidate assertions."""

    validate_authority_evidence(authority)
    validate_pre_merge_evidence(pre_merge)
    validate_complete_evidence(complete)

    authority_hash = evidence_sha256(authority)
    pre_merge_hash = evidence_sha256(pre_merge)
    complete_hash = evidence_sha256(complete)
    authority_repository = _dict(authority["repository"], "authority repository")
    if (
        pre_merge.get("repository") != authority_repository
        or complete.get("repository") != authority_repository
    ):
        raise VerificationError("closure reports name different repositories")

    authority_pull = _dict(authority["pull_request"], "authority pull request")
    authority_stage1 = _dict(authority["stage1"], "authority stage1")
    recomputed_stage1 = _stage1_record(
        repository,
        str(authority_pull["base_sha"]),
        str(authority_pull["head_sha"]),
    )
    if authority_stage1 != recomputed_stage1:
        raise VerificationError("authority report stage1 differs from Git objects")
    if (
        authority_stage1["base_sha"] != authority_pull["base_sha"]
        or authority_stage1["commit_sha"] != authority_pull["head_sha"]
    ):
        raise VerificationError("authority pull request and stage1 are not cross-bound")
    if authority.get("parsed_binding") != authorization_binding(recomputed_stage1):
        raise VerificationError("authority report binding differs from Git objects")

    if pre_merge.get("authority_evidence_sha256") != authority_hash:
        raise VerificationError("pre-merge report does not hash-bind authority report")
    if pre_merge.get("authorization_comment") != authority.get("authorization_comment"):
        raise VerificationError("pre-merge report changes the authorization comment")
    pre_pull = _dict(pre_merge["final_pull_request"], "pre-merge final pull request")
    for key in ("number", "base_ref", "base_sha", "head_ref", "head_repository"):
        if pre_pull.get(key) != authority_pull.get(key):
            raise VerificationError(
                f"pre-merge pull request differs from authority at {key}"
            )
    final_head = _git_sha(pre_pull.get("head_sha"), "control final head")
    final_commit = repository.commit(final_head)
    if final_commit["parents"] != [authority_stage1["commit_sha"]]:
        raise VerificationError("control final head is not the receipt-only child")
    delta = repository.changed_files(str(authority_stage1["commit_sha"]), final_head)
    if (
        len(delta) != 1
        or delta[0]["path"] != RECEIPT_PATH
        or delta[0]["status"] != "A"
    ):
        raise VerificationError("control final head does not add only the receipt")
    receipt_bytes = repository.blob_at(final_head, RECEIPT_PATH)
    try:
        receipt = json.loads(receipt_bytes.decode("utf-8"))
    except (UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise VerificationError("control receipt blob is not UTF-8 JSON") from exc
    authority_comment = _dict(
        authority["authorization_comment"], "authority comment"
    )
    source = str(authority_comment["html_url"])
    expected_artifacts = {
        source.removeprefix("https://"): str(
            authority_comment["body_utf8_sha256"]
        )
    }
    for item in _list(authority_stage1["changed_files"], "stage1 changed files"):
        changed = _dict(item, "stage1 changed file")
        expected_artifacts[str(changed["path"])] = str(changed["new_blob_sha256"])
    _validate_receipt(
        receipt,
        authority=authority,
        expected_artifacts=expected_artifacts,
    )
    receipt_record = _dict(
        pre_merge["receipt_materialization"], "receipt materialization"
    )
    expected_receipt_record = {
        "commit_sha": final_head,
        "parent_sha": authority_stage1["commit_sha"],
        "tree_oid": final_commit["tree"],
        "receipt_path": RECEIPT_PATH,
        "receipt_sha256": _sha256(receipt_bytes),
        "delta_patch_sha256": _sha256(
            repository.deterministic_patch(
                str(authority_stage1["commit_sha"]), final_head
            )
        ),
        "delta": delta,
    }
    if receipt_record != expected_receipt_record:
        raise VerificationError("pre-merge receipt record differs from Git objects")
    final_changed = repository.changed_files(str(pre_pull["base_sha"]), final_head)
    if (
        pre_pull.get("deterministic_patch_sha256")
        != _sha256(
            repository.deterministic_patch(str(pre_pull["base_sha"]), final_head)
        )
        or pre_pull.get("changed_files_manifest_sha256")
        != evidence_sha256(final_changed)
    ):
        raise VerificationError("pre-merge final patch differs from Git objects")

    if complete.get("pre_merge_evidence_sha256") != pre_merge_hash:
        raise VerificationError("complete report does not hash-bind pre-merge report")
    merged = _dict(complete["merged_pull_request"], "complete merged pull request")
    for key in ("number", "base_sha", "head_sha"):
        if merged.get(key) != pre_pull.get(key):
            raise VerificationError(
                f"complete merged pull request differs from pre-merge at {key}"
            )
    merge_sha = _git_sha(merged.get("merge_commit_sha"), "control squash SHA")
    if closure_base != merge_sha:
        raise VerificationError(
            "closure base is not the verified control squash on main"
        )
    merge_commit = repository.commit(merge_sha)
    if merge_commit["parents"] != [pre_pull["base_sha"]]:
        raise VerificationError("control squash does not have the PR base as sole parent")
    if merge_commit["tree"] != final_commit["tree"]:
        raise VerificationError("control squash tree differs from final control head")
    merge = _dict(complete["merge"], "complete merge proof")
    expected_merge = {
        "sole_parent_sha": pre_pull["base_sha"],
        "tree_oid": merge_commit["tree"],
        "final_head_tree_oid": final_commit["tree"],
        "tree_matches_final_head": True,
        "main_contains_merge": True,
    }
    if merge != expected_merge:
        raise VerificationError("complete merge proof differs from Git objects")
    protection_after = _dict(complete["protection_after"], "protection after")
    before = _dict(pre_merge["protection_before"], "protection before")
    if (
        protection_after.get("main_sha") != merge_sha
        or _required_check_set(protection_after)
        != {(name, REQUIRED_APP_ID) for name in FULL_REQUIRED_CHECKS}
        or _protection_state_without_phase_fields(protection_after)
        != _protection_state_without_phase_fields(before)
    ):
        raise VerificationError("complete report does not prove exact policy restoration")
    restore_delay = (
        _parse_time(protection_after.get("observed_at"), "after observed_at")
        - _parse_time(merged.get("merged_at"), "merged_at")
    ).total_seconds()
    if restore_delay < 0 or restore_delay > MAX_RESTORE_DELAY_SECONDS:
        raise VerificationError(
            "complete report exceeds the 600-second restoration window"
        )
    expected_completion_binding = completion_binding(
        pre_merge,
        authority_evidence_sha256=authority_hash,
        merge_commit_sha=merge_sha,
        merge_tree_oid=str(merge_commit["tree"]),
        protection_after=protection_after,
    )
    if complete.get("parsed_completion_binding") != expected_completion_binding:
        raise VerificationError(
            "completion binding does not hash-bind the actual closure reports"
        )
    return {
        "authority": authority_hash,
        "pre_merge": pre_merge_hash,
        "complete": complete_hash,
        "control_final_head": final_head,
        "control_squash": merge_sha,
    }


def verify_evidence_closure(
    repository: GitRepository,
    github: GitHubReader,
    *,
    base_sha: str,
    head_sha: str,
    protection_github: GitHubReader,
    now: datetime | None = None,
) -> dict[str, object]:
    """Verify the evidence-only closure from trusted base-owned code.

    ``repository`` may be a worktree or a bare object repository.  Candidate
    files are treated solely as data; only these three fixed paths are read.
    """

    base_sha = _git_sha(base_sha, "closure base")
    head_sha = _git_sha(head_sha, "closure head")
    repository.commit(base_sha)
    repository.commit(head_sha)
    if not repository.contains(base_sha, head_sha):
        raise VerificationError("closure base is not an ancestor of candidate head")
    delta = repository.changed_files(base_sha, head_sha)
    expected_paths = list(CLOSURE_EVIDENCE_PATHS)
    if (
        [item["path"] for item in delta] != sorted(expected_paths)
        or len(delta) != 3
        or any(
            item["status"] != "A"
            or item["old_mode"] != "000000"
            or item["new_mode"] != "100644"
            or item["old_oid"] != "0" * 40
            for item in delta
        )
    ):
        raise VerificationError(
            "closure base...head delta must add exactly the three regular report files"
        )
    reports: dict[str, dict[str, Any]] = {}
    report_blobs: dict[str, bytes] = {}
    for path in CLOSURE_EVIDENCE_PATHS:
        reports[path], report_blobs[path] = _json_blob_at(
            repository, head_sha, path
        )
    authority = reports[AUTHORITY_EVIDENCE_PATH]
    pre_merge = reports[PRE_MERGE_EVIDENCE_PATH]
    complete = reports[COMPLETE_EVIDENCE_PATH]
    chain = _validate_closure_report_chain(
        repository,
        authority,
        pre_merge,
        complete,
        closure_base=base_sha,
    )

    repository_response = github.get_json(f"/repos/{REPOSITORY}")
    live_repository = _repository_projection(repository_response.data)
    if live_repository != authority["repository"]:
        raise VerificationError("live repository differs from closure reports")
    authority_response, _authority_projection, authority_binding_live = (
        _live_comment_again(github, authority)
    )

    pre_pull = _dict(pre_merge["final_pull_request"], "pre-merge final pull request")
    pull_number = int(pre_pull["number"])
    pull_response = github.get_json(f"/repos/{REPOSITORY}/pulls/{pull_number}")
    pull_payload = _dict(pull_response.data, "live control pull request")
    live_pull = _pull_projection(pull_payload, pull_number, authority=False)
    if any(
        live_pull.get(key) != pre_pull.get(key)
        for key in ("base_ref", "base_sha", "head_ref", "head_sha", "head_repository")
    ):
        raise VerificationError("live control pull request differs from closure reports")
    merged = _dict(complete["merged_pull_request"], "complete merged pull request")
    if (
        pull_payload.get("merged") is not True
        or pull_payload.get("merge_commit_sha") != base_sha
        or pull_payload.get("merged_at") != merged.get("merged_at")
        or merged.get("merge_commit_sha") != base_sha
    ):
        raise VerificationError("live control pull request is not the verified squash")
    main_response = github.get_json(f"/repos/{REPOSITORY}/branches/main")
    main = _dict(main_response.data, "live main branch")
    if _dict(main.get("commit"), "live main commit").get("sha") != base_sha:
        raise VerificationError("live main is not the closure base/control squash")

    completion_comment = _dict(
        complete["completion_comment"], "complete completion comment"
    )
    completion_response = github.get_json(
        f"/repos/{REPOSITORY}/issues/comments/{int(completion_comment['id'])}"
    )
    completion_projection, _completion_body, completion_binding_live = (
        _comment_projection(
            completion_response.data,
            comment_id=int(completion_comment["id"]),
            pull_number=pull_number,
            owner_id=int(live_repository["owner_id"]),
        )
    )
    if (
        completion_projection != completion_comment
        or completion_binding_live != complete["parsed_completion_binding"]
    ):
        raise VerificationError(
            "live owner completion comment/hash chain differs from closure reports"
        )
    if (
        completion_binding_live.get("authority_evidence_sha256")
        != chain["authority"]
        or completion_binding_live.get("pre_merge_evidence_sha256")
        != chain["pre_merge"]
    ):
        raise VerificationError(
            "live owner completion comment does not bind the report hashes"
        )

    live_protection_capture = capture_protection(
        github,
        observed_at=now,
        protection_github=protection_github,
    )
    live_protection, live_protection_raw = _validate_protection_capture(
        live_protection_capture
    )
    expected_protection = _dict(
        complete["protection_after"], "complete protection after"
    )
    left = {
        key: value
        for key, value in live_protection.items()
        if key not in {"observed_at", "raw_response_sha256", "raw_sha256"}
    }
    right = {
        key: value
        for key, value in expected_protection.items()
        if key not in {"observed_at", "raw_response_sha256", "raw_sha256"}
    }
    if left != right or live_protection["main_sha"] != base_sha:
        raise VerificationError(
            "live restored branch protection/rulesets differ from completion report"
        )

    final_head = str(chain["control_final_head"])
    checks_response = github.get_json(
        f"/repos/{REPOSITORY}/commits/{final_head}/check-runs?per_page=100"
    )
    live_checks = _check_runs(checks_response.data, final_head)
    if live_checks != pre_merge["required_check_runs"]:
        raise VerificationError("live control final-head checks differ from pre-merge report")

    return {
        "schema_version": 1,
        "transaction_id": TRANSACTION_ID,
        "phase": "evidence-closure-verification",
        "verified_at": _timestamp(now),
        "closure": {
            "base_sha": base_sha,
            "head_sha": head_sha,
            "delta": delta,
            "report_blob_artifacts": {
                path: _artifact_ref(report_blobs[path])
                for path in CLOSURE_EVIDENCE_PATHS
            },
        },
        "report_object_sha256": {
            "authority": chain["authority"],
            "pre_merge": chain["pre_merge"],
            "complete": chain["complete"],
        },
        "live_owner_provenance": {
            "authorization_binding": authority_binding_live,
            "completion_binding": completion_binding_live,
        },
        "live_control_final_head_checks": live_checks,
        "live_protection_after": live_protection,
        "raw_artifacts": {
            "repository": _artifact_ref(repository_response.raw),
            "authorization_comment": _artifact_ref(authority_response.raw),
            "pull_request": _artifact_ref(pull_response.raw),
            "main": _artifact_ref(main_response.raw),
            "completion_comment": _artifact_ref(completion_response.raw),
            "protection_after": _artifact_ref(live_protection_raw),
            "check_runs": _artifact_ref(checks_response.raw),
        },
    }


def _read_json(path: Path) -> dict[str, Any]:
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        raise VerificationError(f"cannot read JSON {path}: {exc}") from exc
    return _dict(data, str(path))


def _write_json(path: Path, value: object) -> None:
    path = path.resolve()
    path.parent.mkdir(parents=True, exist_ok=True)
    data = json.dumps(value, indent=2, sort_keys=True, ensure_ascii=False).encode("utf-8") + b"\n"
    descriptor, temporary = tempfile.mkstemp(prefix=f".{path.name}.", dir=path.parent)
    try:
        with os.fdopen(descriptor, "wb") as handle:
            handle.write(data)
            handle.flush()
            os.fsync(handle.fileno())
        os.replace(temporary, path)
    finally:
        try:
            os.unlink(temporary)
        except FileNotFoundError:
            pass


def _reader(
    token_env: str,
    *,
    required: bool = False,
) -> LiveGitHubReader:
    token = os.environ.get(token_env)
    if required and not token:
        raise VerificationError(
            f"required GitHub credential environment variable is absent: {token_env}"
        )
    return LiveGitHubReader(token)


def _parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--token-env",
        default="GITHUB_TOKEN",
        help="environment variable containing the ordinary read-only GitHub token",
    )
    parser.add_argument(
        "--protection-token-env",
        default="WP0002_PROTECTION_TOKEN",
        help=(
            "environment variable containing the single-repository, short-expiry "
            "Administration-read token used only by evidence closure"
        ),
    )
    sub = parser.add_subparsers(dest="command", required=True)

    render = sub.add_parser("render-authority-body")
    render.add_argument("--repository-path", type=Path, required=True)
    render.add_argument("--base", required=True)
    render.add_argument("--stage1", required=True)

    authority = sub.add_parser("authority")
    authority.add_argument("--repository-path", type=Path, required=True)
    authority.add_argument("--pr", type=int, required=True)
    authority.add_argument("--comment-id", type=int, required=True)
    authority.add_argument("--stage1", required=True)
    authority.add_argument("--output", type=Path, required=True)

    capture = sub.add_parser("capture-protection")
    capture.add_argument("--output", type=Path, required=True)

    pre = sub.add_parser("pre-merge")
    pre.add_argument("--repository-path", type=Path, required=True)
    pre.add_argument("--authority-evidence", type=Path, required=True)
    pre.add_argument("--protection-before", type=Path, required=True)
    pre.add_argument("--protection-during", type=Path, required=True)
    pre.add_argument("--output", type=Path, required=True)

    completion_body = sub.add_parser("render-completion-body")
    completion_body.add_argument("--repository-path", type=Path, required=True)
    completion_body.add_argument("--pre-merge-evidence", type=Path, required=True)
    completion_body.add_argument("--protection-after", type=Path, required=True)

    complete = sub.add_parser("complete")
    complete.add_argument("--repository-path", type=Path, required=True)
    complete.add_argument("--pre-merge-evidence", type=Path, required=True)
    complete.add_argument("--protection-after", type=Path, required=True)
    complete.add_argument("--completion-comment-id", type=int, required=True)
    complete.add_argument("--output", type=Path, required=True)

    closure = sub.add_parser(
        "verify-evidence-closure",
        help="verify a candidate evidence-only closure using this base-owned verifier",
    )
    closure.add_argument("--repository-path", type=Path, required=True)
    closure.add_argument("--base", required=True)
    closure.add_argument("--head", required=True)
    closure.add_argument("--output", type=Path)
    return parser


def main(argv: list[str] | None = None) -> int:
    args = _parser().parse_args(argv)
    try:
        if args.command == "render-authority-body":
            print(render_authorization_body(GitRepository(args.repository_path), args.base, args.stage1))
            return 0
        github = _reader(args.token_env)
        if args.command == "authority":
            evidence = build_authority_evidence(
                GitRepository(args.repository_path), github,
                pull_number=args.pr, comment_id=args.comment_id, stage1_sha=args.stage1,
            )
            _write_json(args.output, evidence)
        elif args.command == "capture-protection":
            _write_json(args.output, capture_protection(github))
        elif args.command == "pre-merge":
            evidence = build_pre_merge_evidence(
                GitRepository(args.repository_path), github,
                _read_json(args.authority_evidence),
                _read_json(args.protection_before),
                _read_json(args.protection_during),
            )
            _write_json(args.output, evidence)
        elif args.command == "render-completion-body":
            print(render_completion_body(
                GitRepository(args.repository_path), github,
                _read_json(args.pre_merge_evidence),
                _read_json(args.protection_after),
            ))
        elif args.command == "complete":
            evidence = build_complete_evidence(
                GitRepository(args.repository_path), github,
                _read_json(args.pre_merge_evidence),
                _read_json(args.protection_after),
                completion_comment_id=args.completion_comment_id,
            )
            _write_json(args.output, evidence)
        elif args.command == "verify-evidence-closure":
            protection_github = _reader(
                args.protection_token_env,
                required=True,
            )
            evidence = verify_evidence_closure(
                GitRepository(args.repository_path),
                github,
                base_sha=args.base,
                head_sha=args.head,
                protection_github=protection_github,
            )
            if args.output is not None:
                _write_json(args.output, evidence)
        else:  # pragma: no cover - argparse enforces subcommands
            raise VerificationError("unknown command")
    except VerificationError as exc:
        print(f"WP-0002 LOCAL OPERATOR TRANSACTION: FAIL: {exc}", file=sys.stderr)
        return 1
    print("WP-0002 LOCAL OPERATOR TRANSACTION: PASS")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
