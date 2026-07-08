# Single-Package Release Governance For Cauca.ApiClient

* Status: accepted
* Date: 2026-07-07
* Deciders: Cauca.ApiClient maintainers
* Technical Story: Release process parity with sibling libraries

## Context and Problem Statement

The repository publishes a single NuGet package (`Cauca.ApiClient`) to the internal `CaucaNuget` feed. Before this decision, packaging relied on `GeneratePackageOnBuild` and ad-hoc packing/pushing, which is neither repeatable nor previewable and diverges from the release flow used by sibling repositories (e.g. `Cause.SecurityManagement`). A predictable, script-driven release process was needed.

## Decision Drivers

* Repeatable, previewable release (dry run before publish).
* Packaging driven only by the release process, never by an incidental build.
* Parity with the sibling repositories' `release.ps1` + `docs/RELEASING.md` flow.
* Semantic versioning tied to the public fluent API surface.

## Considered Options

* **Option A**: A root `release.ps1` gate (build → test → pack → push to `CaucaNuget`) with `-WhatIf` dry-run support, documented in `docs/RELEASING.md`, and `GeneratePackageOnBuild` disabled so packing is script-only.
* **Option B**: Keep `GeneratePackageOnBuild=true` and push manually.

## Decision Outcome

Chosen option: **Option A**, because a single scripted gate makes releases repeatable and previewable, keeps packaging out of ordinary builds, and mirrors the proven sibling-repo flow. Because only one package ships, the sibling repos' multi-package coordinated-version gate is intentionally simplified out — `release.ps1` reads and echoes the single `<Version>` rather than reconciling several.

### Consequences

* Good: One command previews (`.\release.ps1 -WhatIf`) or performs (`.\release.ps1`) the full release.
* Good: A plain `dotnet build` never emits a package, so builds and releases are cleanly separated.
* Bad: `CaucaNuget` must be a configured NuGet source on the release machine; the script does not configure feeds.
* Bad: Git tagging stays manual, applied only after a successful push.

## Maintenance Invariants
<!-- Behaviors to preserve; this decision is implemented -->
- Keep `release.ps1` as the release mechanism: build with `-p:GeneratePackageOnBuild=false`, run tests (skippable only via the documented `-SkipTests` escape hatch), pack into `./artifacts/nupkg`, and push to `CaucaNuget` guarded by `ShouldProcess` so `-WhatIf` never publishes.
- Keep `<Version>`, `<AssemblyVersion>`, and `<FileVersion>` in `Cauca.ApiClient.csproj` in sync, and bump per the semver rules in `docs/RELEASING.md` against the public fluent API surface.
- Do not re-enable `GeneratePackageOnBuild`; packaging stays script-driven.
- Tag the published commit `v<MAJOR>.<MINOR>.<PATCH>` only after a successful push; `docs/RELEASING.md` is authoritative.
