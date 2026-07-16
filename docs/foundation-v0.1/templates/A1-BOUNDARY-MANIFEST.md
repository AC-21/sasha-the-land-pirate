# A1 Boundary Manifest Authoring Worksheet

This worksheet is not authority. Before an A1 packet runs, a creator-controlled process writes `governance/a1-boundaries/<packet-id>.json` conforming to [`../schemas/a1-boundary-manifest.schema.json`](../schemas/a1-boundary-manifest.schema.json), hashes its raw bytes, stores that exact reference on the packet, and seals one `packet-activation` receipt over the same hash.

## Immutable identity

- Manifest ID:
- Packet ID:
- Packet contract SHA-256:
- Creator/gatekeeper attestor:
- Activation receipt ID:

## Repository and reservation

- Local Git repository root:
- Exact base commit:
- Lease ID and fencing token:
- Expiry:
- Exact reserved paths:
- Exact reserved state domains:

## Approved execution tuple

- Tool name, exact version, source, installer/archive SHA-256 when available:
- OS and exact version:
- Architecture and hardware ID:
- Sandbox profile:
- Network policy:

## Foundation binding

- `00-GAME-CONSTITUTION.md` SHA-256:
- `ledger/decisions.jsonl` SHA-256:
- Last sealed creator receipt ID:

## Protection and credentials

- Protected paths mounted read-only or absent:
- Writable paths (must exactly equal the reservation):
- Approved credential IDs:
- Denied merge, release, protected-main, governance, and receipt-write capabilities:

## Creator import boundary

- Mode: `creator-operated-import-or-reject`
- Agent merge/release/evidence-acceptance capabilities: all `false`
- Creator review required: `true`

## Local observation exceptions

List baseline evidence IDs that cannot be content-addressed before execution. Each must use a concrete `local-observation://` URI; `pending://` is never valid for an active packet. An empty list is preferred.
