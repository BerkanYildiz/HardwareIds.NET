# Changelog

Notable changes to HardwareIds.NET and HardwareIds.Cpp. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/)
and the versions [Semantic Versioning](https://semver.org): a new serialized property is a minor version, a renamed or
removed one a major version. The `Publish` workflow packs the section of the released version as the release notes.

## [2.4.0] - 2026-11-03

### Added

- `library_version`, the last property of every snapshot: the version of the library that produced it, which names the
  JSON schema describing it. `HardwareIds.Version` (.NET) and `HardwareIds::Version()` (C++) expose the same value.
- `Hwid.ToJson()` and the source-generated `HwidJsonContext`: the JSON of `JsonSerializer.Serialize(Hwid)` without
  reflection, for trimmed and Native AOT applications. The library is marked AOT-compatible, and CI compares the snapshot
  of a Native AOT build with the one of the JIT build.

### Changed

- The JSON schema requires `library_version`: a snapshot from 2.3.0 validates against the schema of 2.3.0, not this one.
- The version has a single source, the `VERSION` file at the root of the repository, read by the .NET and C++ builds
  alike; the Publish workflow refuses a tag that does not match it, or that has no dated section in this file.

## [2.3.0] - 2026-11-01

### Added

- TPM capabilities, public keys (endorsement, storage root and persisted keys) and certificates, with the failures of
  the restricted queries listed in `tpms[].errors`.
- `hwid.schema.json`, a strict JSON Schema (draft-07) of the snapshots, attached to each release.

## [2.2.0] - 2026-10-06

### Added

- HardwareIds.Cpp, a C++20 port with a C API that writes the same JSON byte for byte; CI compares the snapshots of both
  libraries on every push.

## [2.1.1] - 2026-09-23

### Fixed

- Physical network adapters require a connector-present interface, as WMI does.

## [2.1.0] - 2026-09-23

### Added

- Richer identifiers: per-disk NVMe, ATA, SCSI VPD and DUID fields, firmware, WWN and partition table GUID; PnP instance
  paths; monitor EDID hash and manufacture date; chassis; Bluetooth radios; batteries; machine GUID, hardware profile
  GUID, install time and machine SID.
- CI and Publish workflows; NuGet.org is reached through Trusted Publishing.

### Fixed

- Processor IDs, physical adapter detection and EDID serials match WMI, the processor name keeps its padding, and
  network adapters that are no longer present are skipped.

## [2.0.0] - 2026-09-16

### Changed

- Hardware information is read directly from the Windows APIs (SMBIOS, storage and NDIS IOCTLs, PnP, EDID, registry,
  spooler, WlanAPI) instead of WMI and wrapper libraries: a scan takes about 10 ms instead of seconds, and the library
  has no third-party dependencies anymore. The public API is unchanged; the targets are .NET 10 and .NET Framework 4.8.
