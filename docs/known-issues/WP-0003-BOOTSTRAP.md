# WP-0003 bootstrap known limits

- `Game` now contains a filtered, hash-bound Unity `6000.5.4f1` donor baseline.
  The canonical folder has not been opened in Unity.
- The curated manifest is approved as a candidate, but its filtered lock has
  not been regenerated or compiled by the canonical Editor. Assistant
  `2.14.0-pre.1` remains a pinned preview risk.
- No scene, prefab, material, volume profile, input asset, camera, debug UI,
  EditMode test, PlayMode test, or visual capture is claimed.
- No agent invoked Unity Hub, Editor, relay, or Unity MCP for this candidate.
- The creator must open the exact repository `Game` project, review package
  resolution and Editor-owned normalization, and satisfy all six first-use
  conditions before Unity MCP work begins.
- The selected donor was open during inspection. Source came from its immutable
  Git commit plus a twice-hash-matched selected patch; the donor was not
  modified. Closing it before the canonical first open remains prudent.
- Donor product/cloud/organization identity is removed. The replacement product
  GUID and bundle namespace are temporary development identity only; durable
  save bytes remain disabled while D-0038 is open.
- Project cloud-service, analytics, diagnostics, collaboration, and cache
  traffic flags are disabled. Global Unity Editor Analytics is account/Editor
  state outside project YAML and remains first-use verification.
- The PC renderer is statically normalized to URP 17.5 asset version 3 from the
  exact installed package migration rule. Canonical Editor import may still
  normalize serialized project bytes and must be reviewed as a new diff.
- SimulationCore and SaveContracts are not linked. Their license and local
  package dependency change remain separately creator-gated.
- The technical canonical string and hash are test oracles, not save bytes.
- Save capture, restore, serialization, migration, slots, paths, and bytes are
  deliberately undefined; the only concrete port rejects persistence.
- GitHub-hosted runner software can drift; the workflow reports its SDK and
  fails rather than installing a replacement.
