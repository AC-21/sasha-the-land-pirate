# Scenario Registry Baseline

All registered scenario definitions, fixture manifests, and materialized artifacts begin at revision `r1` in the initial repository commit. Earlier `r2`/`r3` files were unaccepted A0 working drafts with no commit, protected registry, packet acceptance, or release consumer; they were normalized rather than misrepresented as immutable history.

After the initial commit, a changed definition, fixture manifest, or typed artifact creates `r2` and retains `r1`. Work packets pin the exact definition and fixture-manifest hashes. An artifact-backed fixture manifest uses schema version 2 and binds one materialized starting state, content set, and explicit input script by path, semantic ID, media type, schema version, and SHA-256.
