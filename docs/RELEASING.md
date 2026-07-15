# Releasing Cauca.ApiClient

## Package

This repository publishes a single NuGet package:

| Project | Package |
|---|---|
| `Cauca.ApiClient` | `Cauca.ApiClient` |

The `release.ps1` script at the repository root is the release mechanism. It
reads the `<Version>` element from `Cauca.ApiClient.csproj`, then builds, tests,
packs, and pushes the package to the internal `CaucaNuget` feed. Packing is
driven only by the script — the project intentionally does **not** set
`GeneratePackageOnBuild`, so a plain build never produces a package.

> **Prerequisite:** `CaucaNuget` must be a configured NuGet source on the
> release machine. The script does not configure feeds.

## Semver Bump Rules

Apply the version bump that matches the highest-impact change in the release.

| Change type | Version component | Examples |
|---|---|---|
| Breaking change in any public API, framework support, or behavior contract | **MAJOR** | Remove or rename a public method, change a `FluentBaseService` / `FluentBaseSecureService` signature, **add a parameter (even an optional one) to an existing member**, add a member to a public interface, **drop a target framework**, change a DI extension's contract, alter authentication behavior |
| Additive change (backwards compatible) | **MINOR** | New request-builder method, **new overload**, new DI extension, **new target framework added alongside the existing ones**, mark a member `[Obsolete]` |
| Bug fix, documentation, internal refactor | **PATCH** | Fix incorrect request composition, correct a return type, update XML docs |

> **Adding an optional parameter to an existing member is a MAJOR change, not a MINOR
> one.** Every existing call site still compiles, which makes it look additive, but the
> member's signature changes — consumers compiled against the previous version fail at
> runtime with `MissingMethodException`. Add an **overload** instead and the change is
> MINOR. See
> [Backward Compatibility Is The Default](adr/records/2026-07-14-public-api-compatibility-policy.md),
> which also defines what is required to break compatibility deliberately.

Update `<Version>`, `<AssemblyVersion>`, and `<FileVersion>` in
`Cauca.ApiClient.csproj` to the same new value before releasing.

## Compatibility Gate

The project sets `EnablePackageValidation` with a `PackageValidationBaselineVersion`, so
`dotnet pack` compares the surface being packed against the last published package and
fails the release on a binary-breaking change.

After each successful release, **advance `PackageValidationBaselineVersion` to the
version just published**. If a release intentionally breaks compatibility (MAJOR, with
the ADR and `UPGRADING.md` entry the policy requires), reset the baseline to that new
version — otherwise the gate keeps failing on a break that was already approved.

## Release Notes

The package carries a `<PackageReleaseNotes>` element in
`Cauca.ApiClient.csproj`. When releasing, describe what changed and any
compatibility expectations for consumers.

Example:

```xml
<PackageReleaseNotes>
4.2.0 — Adds &lt;feature&gt;. Backwards compatible with 4.1.x consumers.
</PackageReleaseNotes>
```

## How to Release

1. Bump `<Version>`, `<AssemblyVersion>`, and `<FileVersion>` in
   `Cauca.ApiClient.csproj` to the new value.
2. Update `<PackageReleaseNotes>` in the same `.csproj` to describe the release.
3. Commit, push, and merge to the `main` branch.
4. Run a dry run to preview what will be pushed — nothing is published:

   ```powershell
   .\release.ps1 -WhatIf
   ```

5. Run the full release when ready:

   ```powershell
   .\release.ps1
   ```

6. Tag the released commit and push the tag. Use the `v<MAJOR>.<MINOR>.<PATCH>`
   convention (e.g. `v4.1.0`), matching the `<Version>`. Tag the exact commit on
   the `main` branch that was published:

   ```powershell
   git tag -a v4.1.0 -m "Release 4.1.0 (final, non-experimental)"
   git push origin v4.1.0
   ```

   Pre-release versions (`4.2.0-beta1`, `-preview*`, etc.) are not tagged; only
   finalized versions get a `v*` tag.

`release.ps1` is the release mechanism. It will:
- Read and display the package `<Version>`.
- Build the solution in Release configuration (`GeneratePackageOnBuild` disabled).
- Run all tests (use `-SkipTests` only as a documented escape hatch when tests
  were already run locally in this session).
- Pack `Cauca.ApiClient` into `./artifacts/nupkg`.
- Push the produced `.nupkg` to the `CaucaNuget` feed.

It does **not** create the git tag — tagging is a manual step (6 above) so the
tag is only applied once the push has actually succeeded.
