# SaveContracts

WP-0003 defines only a disabled persistence capability:

- report `SaveCapability.Disabled`;
- reject persistence with `WP0003_PERSISTENCE_DISABLED`;
- report zero bytes written.

This package intentionally contains no file, directory, stream, serializer,
database, cloud, slot, envelope, migration, or byte-layout implementation.
It exposes no snapshot, capture, restore, serializer, path, stream, version,
migration, envelope, or byte-schema API. Those choices require a later
separately reviewed save packet.
The folder is a local Unity package shell but remains unlinked until the
creator approves the offline `Game` dependency change.
