# WP-0001 creator-operated A1 activation runbook

Status: **draft / fail closed / not activation authority**

This runbook turns the accepted WP-0001 contract into an inspectable creator
procedure. It does not authorize an agent or CI runner to invoke Unity, Hub,
the Editor executable, CLI, or batchmode. It does not activate A1.

Read first:

- `AGENTS.md`
- `docs/foundation-v0.1/06-AGENT-OPERATING-MODEL.md`
- `docs/foundation-v0.1/11-TRUST-AND-ENFORCEMENT.md`
- `docs/foundation-v0.1/work-packets/proposed/WP-0001.json`
- `docs/evidence/WP-0001/pre-a1-readiness-20260716.json`
- `docs/evidence/WP-0001/UNITY-MCP-STATIC-SECURITY-AUDIT-20260716.md`

## Stop conditions

Stop without opening a Unity MCP session if any of these is true:

- protected `main` changed after the candidate base was frozen;
- the candidate shares `.git`, alternates, credentials, or writable protected
  state with the trusted checkout;
- the creator project seed is not content-addressed;
- the Editor/package/toolchain tuple differs;
- the seat or project/organization link is unverified;
- Unity’s persistent relay is externally reachable;
- client allowlist, target, PID, or private runtime environment is absent;
- the clean setup cycle would run a model prompt or Unity tool;
- any exact binary, policy, configuration, or evidence hash drifts;
- a secret value would enter Git;
- the activation receipt or reservation is incomplete.

## 1. Freeze the final protected base

Creator or trusted gatekeeper records:

```bash
git status --short --branch
git rev-parse HEAD
git log -5 --oneline --decorate
python3 -B docs/foundation-v0.1/tools/validate_foundation.py
shasum -a 256 \
  docs/foundation-v0.1/00-GAME-CONSTITUTION.md \
  docs/foundation-v0.1/ledger/decisions.jsonl \
  docs/foundation-v0.1/work-packets/proposed/WP-0001.json
```

The immutable packet contract is:

`eed333603affe6aa1dd2b16b26ae702d9f561cc653fa319da02abfe008faeda3`

Raw file hashes are not contract hashes and must be recalculated after every
protected change. Recreate the candidate after this runbook PR merges; do not
reuse the earlier prepared clone.

## 2. Establish the physical quarantine

The creator creates a dedicated ephemeral standard macOS user, or a proven
equivalent native OS sandbox. Unity is not supported in a container for this
proof; a directory named `sandbox` is not sufficient.

Create a detached independent clone from the frozen base:

```bash
git clone --no-local --no-hardlinks --no-checkout <read-only-source> <candidate>
git -C <candidate> checkout --detach <approved-base>
git -C <candidate> remote remove origin

test -d <candidate>/.git
test ! -f <candidate>/.git
test ! -f <candidate>/.git/objects/info/alternates
git -C <candidate> rev-parse HEAD
git -C <candidate> rev-parse --git-common-dir
git -C <candidate> status --porcelain=v1
git -C <candidate> remote -v
```

Required oracle:

- detached at the receipt-bound base;
- independent `.git` directory;
- no shared worktree or alternates;
- no writable remote;
- no GitHub, merge, release, governance, or receipt credential;
- trusted checkout and creator HOME are not writable.

Capture from the runtime principal:

```bash
id -un
id -u
realpath "$HOME"
realpath "$(getconf DARWIN_USER_TEMP_DIR)"
realpath /tmp
printenv HOME TMPDIR TMP TEMP CODEX_HOME XDG_CONFIG_HOME XDG_CACHE_HOME \
  XDG_DATA_HOME GIT_CONFIG_NOSYSTEM GIT_CONFIG_GLOBAL GIT_TERMINAL_PROMPT
env | sed 's/=.*//' | sort
test ! -w /Users/sasha
test ! -w /private/tmp
```

Do not capture the full environment with values. The second command records
names only. `HOME`, `TMPDIR`, `TMP`, and `TEMP` must equal the manifest roots.
`CODEX_HOME` and all XDG/Git overrides must resolve under the same disposable
HOME; `GIT_CONFIG_NOSYSTEM=1` and `GIT_TERMINAL_PROMPT=0`. The guarded
credential variables named by the manifest must be absent, including empty
assignments. Record resolved `/private/...` paths; `/tmp` itself is a symlink
and is not a canonical runtime root.

Hash the exact sandbox and network policies:

```bash
shasum -a 256 <sandbox-policy> <network-policy>
```

The policies must deny:

- creator-HOME writes and all shared-temp writes except the exact Bridge socket;
- protected checkout and approval-record writes;
- non-loopback inbound access;
- unapproved network egress;
- symlink escape;
- credential/keychain inheritance not explicitly named.

## 3. Bind the creator-created project seed

WP-0001 requires the temporary Unity project and Editor-generated package locks
before agent implementation. Do not leave these bytes as an untracked,
unhashed dirty baseline.

The only accepted mode is a creator-created protected setup commit. A dirty
candidate-only seed is not executable authority.

The protected seed may contain only:

- `Game/Packages/manifest.json`;
- `Game/Packages/packages-lock.json`;
- `Game/ProjectSettings/`;
- an optional empty `Game/Assets/.gitkeep` or `.keep`.

It contains no scripts, scenes, prefabs, shaders, models, production assets,
embedded package tree, local package, `file:` dependency, Git dependency,
tarball, or non-Unity registry source. Every direct and transitive package is a
`com.unity.*` registry/builtin record. The creator binds the complete manifest
and lock graph, not only the three headline packages.

The creator commits
`docs/evidence/WP-0001/a1-activation/project-seed.json` in the same protected
setup commit. That record binds the four required seed files, full dependency
graph, Editor version/changeset, exact temporary identity, and the attestation
that no implementation exists.

After the commit:

```bash
git ls-tree -r -z <protected-base> -- Game | shasum -a 256
git show <protected-base>:Game/Packages/manifest.json
git show <protected-base>:Game/Packages/packages-lock.json
git show <protected-base>:docs/evidence/WP-0001/a1-activation/project-seed.json
```

Freeze that new protected base and recreate the detached candidate from it.
The protected `.codex/config.toml` stays disabled and is not rewritten by the
runtime route.

## 4. Creator installs the exact D-0047 tuple

Only the creator operates Hub and Editor UI.

Required tuple:

- Unity Hub `3.19.5`
- Unity Editor `6000.3.19f1`
- changeset `7689f4515d75`
- ARM64 Editor
- Mac Build Support (IL2CPP) for that exact Editor
- Xcode `26.3`
- Rosetta 2
- .NET SDK `10.0.301`
- `com.unity.ai.assistant` `2.14.0-pre.1`
- URP `17.3`
- Unity Test Framework `1.6`

Before opening Hub or Editor, the creator may run the A0-only static diagnostic:

```bash
python3 -B docs/foundation-v0.1/tools/inspect_wp0001_toolchain_static.py \
  --project-root "/absolute/path/to/detached-candidate/Game" \
  --output "/absolute/path/outside-a1-activation/static-toolchain.json"
```

The collector reads bounded plist, JSON, Mach-O, receipt, and package bytes. It
starts no Unity-family or external process and performs no network access.
Exit `0` means the static tuple matched, `1` means blocked or indeterminate,
and `2` means collection failed. Even exit `0` remains non-authoritative A0
diagnostic evidence. Mac IL2CPP remains `unverified` until a protected physical
marker profile is supplied; the repository intentionally ships no such profile.

Unity’s release page identifies the exact Editor, changeset, ARM64 installer,
Mac IL2CPP component, and a known Metal timeout/freeze issue that WP-0001 must
exercise:
<https://unity.com/releases/editor/whats-new/6000.3.19f1>.

Secret-free non-Unity checks the creator may capture:

```bash
xcodebuild -version
xcode-select -p
dotnet --version
dotnet --list-sdks
pkgutil --pkg-info com.apple.pkg.RosettaUpdateAuto
file "<Unity executable path>"
shasum -a 256 Game/Packages/manifest.json Game/Packages/packages-lock.json
```

Required screenshots:

1. Hub installation showing exact Editor and Mac IL2CPP module.
2. Editor About showing version and changeset.
3. Package Manager showing Assistant, URP, and Test Framework.
4. Assigned eligible Unity AI seat and organization.
5. Temporary project linked to the same organization.
6. Player Settings showing the temporary identity.

Temporary identity:

- Company: `LocalFoundationLab`
- Product: `SashaAtomicLandPirate_WP0001`
- Bundle ID: `local.foundation.sashaatomiclandpirate.wp0001`
- Profiles: `wp0001-dev-v1`, `wp0001-test-v1`
- no prior-root discovery or migration

Crop/redact account captures. Store sensitive identifiers only as hashes or
secret-free references.

## 5. Configure the direct MCP route inside quarantine

Do not use Unity’s one-click Codex configuration as final evidence; it writes
only `--mcp`.

The runtime’s only active Codex MCP configuration is
`<ephemeral-home>/.codex/config.toml`. The protected candidate-local
`.codex/config.toml` remains disabled. The runtime config includes:

- exact relay executable inside the ephemeral runtime HOME;
- `--mcp`;
- exact absolute `--project-path <candidate>/Game`;
- current `--instance-id <editor-pid>`;
- `required = true`;
- exact `enabled_tools` allowlist;
- SHA-256 of the canonical JSON allowlist;
- creator claim `AUTHORIZE-WP0001-MCP-ALLOWLIST` on the activation receipt;
- exact repository protocol, network, and policy-attachment collector paths
  and source hashes;
- creator claim `AUTHORIZE-WP0001-RAW-COLLECTORS` on the activation receipt;
- creator claim `AUTHORIZE-WP0001-CODE-IDENTITIES` over the exact strict
  Identifier, TeamIdentifier, CDHash, designated-requirement hash, and
  authority-list hash for the client, relay, and Editor;
- prompt-based default and per-tool approval;
- private `HOME`, `TMPDIR`, `TMP`, and `TEMP`;
- isolated `CODEX_HOME`, XDG config/cache/data roots, and Git global/system
  override paths;
- absent GitHub, SSH-agent, Vercel, and AWS credential variables;
- canonical hashes of the exact client environment and effective MCP server
  inventory, bound by the client, preflight, and activation session;
- no global Unity MCP fallback;
- no undeclared server.

Use client-visible sanitized names, such as `Unity_ReadConsole`, not Unity’s
dotted internal names. A1 categorically excludes `Unity_RunCommand`,
`Unity_PackageManager_ExecuteAction`, and `Unity_ImportExternalModel`. Adding
one requires a separately protected boundary revision; it cannot be smuggled
into activation.

OpenAI’s current config reference documents project-scoped MCP configuration,
`enabled_tools`, `disabled_tools`, `env`, `required`, and ordered command/argument
identity constraints:
<https://developers.openai.com/codex/config-reference/>.

The exact post-activation tool list is still unresolved. Do not invent it from
the Unity UI. The creator chooses and binds one reviewed list in the activation
receipt and manifest. That list:

- excludes arbitrary shell/C# execution unless separately justified;
- excludes package/version mutation after the package graph is frozen;
- exposes only tools required by WP-0001;
- uses client-side allowlisting even if Unity advertises fewer tools;
- is re-evaluated on any package or Codex update.

## 6. Neutralize the persistent Assistant relay

The stock package starts a separate persistent relay with a client REST port.
It must not remain externally reachable.

Acceptable evidence is either:

1. an immutable creator bootstrap proved to suppress persistent relay
   auto-start and lazy re-entry on the exact package; or
2. a hash-bound OS policy that denies non-loopback inbound traffic, with a
   live listener test demonstrating the boundary.

Creator captures a complete process/listener census in either mitigation mode.
The raw probe record must include at least one of each:

- non-loopback listener probe that fails;
- loopback control probe that succeeds;
- approved-egress probe that succeeds;
- unapproved-egress probe that fails.

The probe record binds the boot session, policy SHA-256, capture time, every
listener row, target, outcome, and raw-row hash. A zero-valued summary without
those records cannot pass.

Fail if a wildcard/all-interface listener is reachable outside quarantine.
Do not patch the signed relay binary.

The direct Bridge uses a Unix-domain socket even though Unity’s discovery
record labels it `named_pipe`. Its exact path is:

`/tmp/unity-mcp-<first-8-SHA1-of-absolute-Game/Assets-path>-<editor-pid>`

Shared temp is default-denied except the one resolved `/private/tmp/...`
socket path, with creator-observed owner UID and mode `0600`.

## 7. Run the clean zero-tool-call connection cycle

The creator performs this sequence with no model/agent prompt:

1. Close the old observation project or stop its Bridge.
2. Revoke the old project’s Codex access and disconnect it.
3. Confirm the new project has no inherited connection history.
4. Disable **Auto-approve in Batch Mode**.
5. Confirm the client allowlist and OS/network policy are already active.
6. Start the exact client against exact project path and current Editor PID.
7. Review the first pending connection inside Unity.
8. Allow that exact connection.
9. Permit only protocol initialization and tool discovery.
10. Invoke zero Unity tools.
11. Disconnect.
12. Revoke the preflight approval and confirm connection history is absent.

Then establish the distinct activation session:

1. Start or reconnect the exact receipt-bound client/relay.
2. Confirm no approval history exists before this connection.
3. Creator-approve the exact connection.
4. Capture client/relay/Editor PIDs, process start times, boot-session-bound
   process birth IDs, config/allowlist/environment/inventory hashes,
   connection record, Editor/relay Unix FD peer, reciprocal client/relay
   stdin/stdout pipes, and the derived session ID.
5. Keep this activation session connected with zero model prompts and zero
   Unity tool calls.
6. Run the read-only route verifier against the already-running processes:

   ```bash
   python3 -B docs/foundation-v0.1/tools/validate_wp0001_mcp_live.py \
     --boundary docs/foundation-v0.1/governance/a1-boundaries/WP-0001.json \
     --output docs/evidence/WP-0001/a1-activation/commands/mcp-route-live.json
   ```

   It must report `PASS`. It reads OS/process/config/discovery/socket state and
   never starts Unity, Hub, relay, Codex, or MCP. Its
   `route_contract_sha256` covers the stable boundary projection while
   deliberately excluding evidence references that hash the capture itself;
   this prevents a circular fixed-point manifest.
7. Materialize the boundary manifest and issue the activation receipt within
   five minutes of the session capture.

WP-0001 may use only that live session. Disconnect, PID reuse, process restart,
config drift, allowlist drift, package drift, or target drift immediately
invalidates A1 and requires a new clean cycle plus creator activation receipt.

Because the package permits tool calls while approval is pending, the pending
window is acceptable only inside the already-proven isolated principal and
network boundary.

Required evidence:

- exact relay path/version/SHA-256, strict component-signature verification,
  Identifier, TeamIdentifier, CDHash, designated-requirement hash, authority
  list hash, PID, start time, and boot-session-bound process birth ID;
- exact Codex path/version/SHA-256/publisher metadata, the same strict
  component-signature tuple, PID, start time, and process birth ID;
- exact arguments, target, PID, environment, allowlist, and approval policy;
- bridge connection file, socket, owner/mode, target, and SHA-256;
- no global Unity MCP entry;
- no externally reachable persistent relay;
- preflight first-connection screenshot, disconnected/revoked final state, and
  complete handshake log;
- distinct live activation-session record and connected final state;
- model prompts: `0`;
- Unity tool invocations: `0`;
- all three prior deviations retained and ineligible.

“Zero tool call” means zero invocation; it does not mean the handshake must
advertise zero tools.

## 8. Materialize activation evidence

Suggested creator-import tree:

```text
docs/evidence/WP-0001/a1-activation/
  evidence-manifest.json
  project-seed.json
  toolchain.json
  entitlement-linkage.json
  project-identity.json
  quarantine.json
  mcp-route.json
  bridge-discovery.json
  codex-runtime-config.toml
  clean-handshake.json
  activation-session.json
  network-observation.json
  deviations.json
  sandbox.policy
  network.policy
  commands/
    quarantine-live.json
    mcp-route-live.json
    clean-handshake.raw.json
    activation-session-live.json
    network-probes.json
  screenshots/
```

Run the non-Unity live verifier as the isolated runtime principal:

```bash
python3 -B docs/foundation-v0.1/tools/validate_wp0001_a1_live.py \
  --boundary docs/foundation-v0.1/governance/a1-boundaries/WP-0001.json \
  --output docs/evidence/WP-0001/a1-activation/commands/quarantine-live.json
```

It must report `PASS`. It checks the actual candidate root, independent Git
directory/common directory, exact detached base, zero remotes/alternates,
clean tree, isolated UID/environment, protected-root denial, and absent
Git/release credential environment. It never starts Unity, Hub, Codex, relay,
or MCP. The protected checkout is taken only from the receipt-bound
`repository.trusted_root`; there is no command-line override.

Every retained artifact records:

- relative path;
- media type and byte size;
- SHA-256;
- capture time and producer;
- source command or UI location;
- known limitations;
- secret-redaction status.

Every typed JSON record names non-empty raw source artifacts. Those bytes must
exist, match their hashes and sizes, and appear in `evidence-manifest.json`.
The route and Bridge records must also bind the exact
`validate_wp0001_mcp_live.py` source; quarantine evidence binds
`validate_wp0001_a1_live.py`. Protocol and network records use the fixed file
names above and must satisfy the strict event/listener/probe parsers in
`validate_foundation.py`.
The activation receipt binds the evidence manifest, so a typed restatement
without raw command, UI, Git, process, or protocol sources cannot pass.

The canonical boundary manifest remains:

`docs/foundation-v0.1/governance/a1-boundaries/WP-0001.json`

It must bind:

- packet contract and exact candidate base;
- content-addressed project seed;
- held reservation over every declared path/domain;
- exact toolchain and environment;
- runtime principal/UID/HOME/temp and ambient denials;
- sandbox/network policy hashes;
- current constitution/ledger and prior sealed creator receipt;
- exact protected, writable, and scratch paths;
- exact route evidence and clean handshake;
- distinct live activation session plus creator-authorized allowlist claim and
  digest;
- exact content-addressed protocol, network, and policy-attachment collectors
  plus their creator authority claim;
- denied merge/release/protected/governance/receipt capabilities;
- creator-operated import-or-reject boundary.

Exact permitted scratch:

- `Game/Library/`
- `Game/Temp/`
- `Game/Logs/`
- `Game/Obj/`
- `Game/UserSettings/`
- `Game/MemoryCaptures/`
- `Game/Recordings/`

`Game/Builds/` is not scratch. Build evidence goes directly to
`BuildArtifacts/WP-0001/`.

## 9. Protected activation transaction

The creator issues one distinct sealed packet-activation receipt, for example:

`RR-WP0001-ACTIVATE-YYYYMMDD`

Required receipt properties:

- `receipt_kind: packet-activation`
- `issuer_role: creator`
- subject only `WP-0001`
- claims:
  - `A1-QUARANTINE-BOUNDARY-VERIFIED`
  - `ACTIVATE-A1-WP-0001`
  - `AUTHORIZE-WP0001-MCP-ALLOWLIST`
  - `AUTHORIZE-WP0001-RAW-COLLECTORS`
  - `AUTHORIZE-WP0001-CODE-IDENTITIES`
- exact packet-contract hash
- exact raw boundary-manifest hash
- exact activation-evidence manifest hash
- exact current constitution and decision-ledger hashes
- last sealed creator receipt that existed before activation
- external protected signature/reference

The three collector implementations named by the manifest must exist on the
protected base before this receipt is issued. Their raw source hashes must
match both the boundary and every raw capture. At the time of this A0 runbook,
those implementations are not yet present; this is an intentional activation
blocker, not permission to substitute ad hoc scripts.

The protected control-plane transaction must atomically:

- set the `technical_spike` gate to `passed`;
- attach the activation receipt to its quarantine requirement;
- set `active_autonomy` to `A1`;
- set `active_a1_packet_id` to `WP-0001`;
- populate current authority hashes;
- set WP-0001 reservation to `held`;
- reserve exactly all declared paths and affected domains;
- set lease, fencing token, base, and future expiry;
- attach the boundary-manifest reference/hash;
- transition `accepted -> active`;
- preserve the one activation receipt through later active lifecycle events.

Then run:

```bash
python3 -B docs/foundation-v0.1/tools/validate_foundation.py
python3 -B -m unittest discover -s docs/foundation-v0.1/tools -p 'test_*.py'
git diff --check
git status --short
```

Only the creator may merge the activation transaction. The detached candidate
does not pull that commit, obtain protected credentials, or merge its own work.

## Current result

Activation remains blocked. The runbook is complete enough to expose the
remaining work, not to waive it:

- exact D-0047 installation is absent;
- seat and linkage remain unverified;
- physical sandbox/network policies are not materialized;
- protected creator project seed is not yet created;
- exact post-activation allowlist is not ratified;
- exact client/relay/Editor signing identity tuples are not yet creator-ratified;
- exact collectors do not yet exist and therefore cannot be source-hash
  authorized;
- `RAW-CAPTURE-PROVIDER-DECISION.md` records the advisory finding that the
  installed relay/Bridge diagnostics do not provide exact raw protocol frames
  and request IDs. Schema v4 does not yet bind independent providers or retained
  raw bytes, so a protected successor is required before provider independence
  can become an activation gate;
- persistent-relay mitigation is not proven;
- no fresh live capture yet proves the exact Editor-owner/relay-peer Unix FD
  and reciprocal Codex/relay stdin/stdout pipe graph;
- sandbox/network policy files are not yet proven attached to the isolated
  principal and the exact running client, relay, and Editor processes;
- the verifier binds a stable executable pathname/vnode and strict signature,
  but kernel-loaded-image identity and in-memory cloud-managed Codex
  requirements are not yet independently attested;
- the currently installed ChatGPT/Codex component and Unity relay component
  both fail `/usr/bin/codesign --verify --strict`; displayed signer metadata
  cannot substitute for a valid strict signature, so clean approved installs
  or a separately creator-ratified hash-only/quarantine exception are required;
- activation evidence bytes, boundary manifest, live session, and receipt do
  not exist.

The local validator can require an `external-protected` receipt reference and
exact claims/hashes, but it cannot authenticate GitHub's external state by
itself. The creator-controlled protected transaction remains the authority.

Keep A0, WP-0001 `accepted`, the protected repo MCP entry disabled, and Unity
tool calls at zero until every condition is closed.
