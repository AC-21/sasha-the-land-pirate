# VGR-13 performance investigation

Status: diagnostic record; the performance gate remains open. This is not
governance, a packet amendment, a release disposition, a native-performance
waiver, or evidence that VGR-13 or V0 has passed.

The accepted VGR-13 visual contract remains byte-identical at SHA-256
`a6058ded7386a7cbca5208db89b193ab2917c5790ea0c97add465998f3b0953f`.
This note records how its presentation-local proof is separated from the
repository-wide native-player gate without weakening either one.

## Diagnostic finding

On the creator Mac in Unity 6000.5.4f1 Editor Play Mode, the whole-loop
`GC Allocated In Frame` counter was sampled for two 30-second diagnostics after
a 10-second warm-up:

| Run | Samples | Zero samples | p95 | Average | Maximum |
|---|---:|---:|---:|---:|---:|
| Explicitly paused | 22,038 | 1 | 9,901 B | 10,809.528 B | 203,983 B |
| Unpaused | 21,593 | 1 | 9,939 B | 10,845.590 B | 530,513 B |

Both authorized PlayMode runs passed all 40 discovered tests. Their dispatcher
receipts are local Unity artifacts
`wp0002-playmode-test-assembly-4c9d4acc9604458b892604eea91d256b.json`
and
`wp0002-playmode-test-assembly-d0332ed0181b42339b3c1bc77da10c8c.json`.

The near-identical paused and unpaused distributions do not identify an
allocation source. Both runs keep the Field Desk and UI Toolkit active, while
also including Unity Editor, Test Runner, and existing simulation work. The
result is therefore inconclusive for Field Desk attribution and fails to prove
either required threshold. Editor Play Mode is not the repository's authority
for the standalone whole-application target-Mac threshold.

## Useful but non-authoritative diagnostics

The ordinary PlayMode assembly now checks all of the following:

- after the one-second deadline expires, one unchanged Field Desk stamp check
  allocates exactly `0 B` on the executing managed thread;
- 10,000 consecutive unchanged per-frame refresh calls allocate exactly `0 B`
  on that thread;
- 100 garage-to-city handoff cycles retain the same one Field Desk,
  `UIDocument`, `PanelSettings`, visual tree, 18-object binding array, camera
  set, and `AudioListener` set;
- the first post-cycle pause submit enqueues exactly one command and a duplicate
  same-frame submit remains latched out;
- every binding still reports registered at the checkpoints, the desk owns exact city
  overview, exactly one mode root is active, and canonical bytes do not change
  during the presentation-only cycle.

These are useful regression checks, but they do not measure allocations from UI
Toolkit layout/render work between calls, managed or native retained-memory
trend, callback cardinality, or the unpaused standalone player. They therefore
cannot replace the unchanged contract. A later 43/43 ordinary PlayMode run also
passed after adding a request-gated five-minute direct-call soak. The first
requested soak stopped at NUnit's 180-second default timeout before producing a
metric result; its receipt is
`wp0002-playmode-test-assembly-60181835ebae42e2823a2c9aaddf553c.json`.
After declaring a six-minute timeout, the fresh request completed
`300.003 s`, sampled `168,026` direct refresh calls, observed a maximum direct
delta of `0 B`, retained 77 visual elements and 18 button bindings, and passed
all 43 discovered tests. That successful soak still has the same attribution
limit and is not release evidence. The ordinary and requested-soak dispatcher
receipts are respectively
`wp0002-playmode-test-assembly-1a76978142994c079e1d4a212506811f.json`
and
`wp0002-playmode-test-assembly-640312bf49164754b1734c5614048cdd.json`.
The ordinary run used the current test source whose SHA-256 is
`0a10f1f734289f8f2050e18f0c5a3faa78db47729d74a6f3c385694a22b2f1a7`;
the requested soak used its immediately preceding source at SHA-256
`34a3403235d1aed9f0c824a3c9eeb924853d564e3fdec399a4d05f44745d1c9f`.
Those local receipts do not contain repository commit/tree binding or raw
metric samples, so they remain diagnostic records rather than authoritative
source-bound evidence.

## Remaining exact gate

VGR-13 must remain unreleased until evidence covers the contract as written:

- a five-minute paused, unchanged city-overview run with Field Desk and UI
  Toolkit frame work included, p95 `0 B/frame`, and no retained growth;
- a five-minute representative unpaused whole-application run with the UI
  included, p95 `0 B/frame` and average `< 1,024 B/frame`;
- 100 overview handoffs with managed and native memory trend, detached-panel
  leakage, callback cardinality, and a post-cycle one-action check;
- source-bound request, player, instrumentation, metric, commit/tree, Unity,
  and protected-check evidence.

The authoritative whole-application run must use an authorized standalone
native ARM64 player. The broader V0 target-Mac run must additionally cover RSS,
Metal memory, frame time, and drift. A failure must remain visible; it cannot be
hidden in Editor noise, repaired by forcing an artificial frame rate, or
reclassified as a presentation-only pass.
