# Backward Compatibility Is The Default For The Public API And Target Frameworks

* Status: accepted
* Date: 2026-07-14
* Deciders: Cauca.ApiClient maintainers
* Technical Story: Signature and target-framework breaks shipped in 4.0.0 (`cfc7e9c`)

## Context and Problem Statement

`Cauca.ApiClient` is consumed as a NuGet package by every service that talks to an
internal API, so one break in its surface is paid for once per consumer. The repository
has no stated compatibility policy: "maximum backward compatibility unless explicitly
stated" has been an unwritten assumption, which means there is nothing for a reviewer to
hold a change against and nothing for the build to enforce.

Release 4.0.0 (`cfc7e9c`, "Support cancellation token everywhere + Bump to DotNet 10")
is the motivating example. It predates this repository's governance — ADRs, `AGENTS.md`
and `docs/RELEASING.md` all landed later, in `c656f59` — so it broke no rule that
existed at the time. It is useful precisely because it shows, in one commit, the two
break modes we now want to rule out:

**1. A signature mutated where an overload would have done.** A
`CancellationToken cancellationToken = default` parameter was appended to the existing
members of `IBaseService`, `BaseService` and `BaseSecureService`. Every existing call
site still compiled, so the change *looked* backward compatible — but appending a
parameter changes a member's signature. Assemblies compiled against 3.3.3 that resolve
4.x at runtime fail with `MissingMethodException`, and every consumer implementing or
mocking `IBaseService` had to be edited. Adding token-bearing overloads alongside the
originals would have cost nothing and preserved both.

**2. A target framework replaced rather than added.** The same commit moved `net9.0` to
`net10.0`. That locks out every consumer still on .NET 9 regardless of API surface, and
because it was bundled with the token change, no one could adopt either independently.

The version bump itself was correct — 3.3.3 to 4.0.0 signalled a major. The lesson is
narrower: **a major version was treated as licence to break contracts that did not need
breaking.** This ADR makes compatibility the explicit default, and adds a mechanical gate
so the binary-break case cannot ship unnoticed.

## Decision Drivers

* A single package fans out to every consuming service; each break is multiplied.
* Source compatibility is easy to eyeball and gives false confidence. Binary
  compatibility is the property that actually fails at runtime, and it is invisible to
  review — the reviewer's evidence ("everything still compiles") is precisely the
  misleading signal.
* Optional parameters and interface-member additions are the specific traps: they
  compile cleanly at every existing call site while breaking the ABI.
* Consumers upgrade on their own schedule and resolve transitively. They must be able to
  adopt a framework bump and an API change independently.
* A policy that the build does not enforce decays into a wish.

## Considered Options

* **Option A**: State compatibility as the default in an ADR — distinguishing source,
  binary and behavioral compatibility; requiring additive evolution (overloads, not
  signature mutation) and additive framework support (add a TFM, don't replace it) even
  within a major — and enforce the binary case with the .NET SDK's package validation
  against the last published version.
* **Option B**: Document the rules in `docs/RELEASING.md` and rely on review.
* **Option C**: Guarantee source compatibility only; accept binary and TFM breaks as
  normal, on the assumption consumers rebuild from source anyway.

## Decision Outcome

Chosen option: **Option A**.

Option B cannot catch a binary break, because prose cannot make the reviewer see what
compiles-but-does-not-link. It also leaves the rule discoverable only by people who
already know to look. Notably, `docs/RELEASING.md` today still lists "new optional
parameter" as a backwards-compatible MINOR change — it did not cause the 4.0.0 break
(it did not yet exist), but left alone it would license the next one.

Option C was rejected because it is not true of our consumers. They do not all rebuild
in lockstep, so a binary break surfaces as a runtime `MissingMethodException` in a
service nobody thought they were changing — the worst possible place to find one.

The policy:

**1. The three levels of compatibility are distinct, and all three are in scope.**

| Level | Broken by | Symptom |
|---|---|---|
| Source | Removing or renaming a member; changing a parameter type | Consumer fails to compile |
| Binary | **Adding a parameter — even an optional one — to an existing member**; adding a member to a public interface; changing a return type | Consumer compiles, then throws `MissingMethodException` / `TypeLoadException` at runtime |
| Behavioral | Same signature, different semantics (retry, auth, thrown exception types) | Consumer compiles and runs, and is silently wrong |

Source compatibility is **not** evidence of binary compatibility. "Every call site still
compiles" must never be accepted as proof that a change is safe.

**2. Evolve the API additively: prefer an overload over mutating a signature.**

Adding a parameter to an existing public or virtual member is a break — add an overload
that delegates to the existing one instead. Adding a member to a public interface is
likewise a break; add a default implementation, or a new interface. This holds **even in
a major release**: a major bump is permission to break when there is no alternative, not
licence to break when there is one. The 4.0.0 cancellation-token change would have been
fully compatible as overloads, in the same major, at no cost.

**3. Support target frameworks additively: add a TFM, do not replace it.**

Shipping support for a new .NET version means **adding** it to `<TargetFrameworks>`,
keeping the previous one, so consumers can adopt the package without also being forced
to retarget. *Dropping* a framework is the breaking change, and needs the escape hatch
below. Do not bundle a framework change with unrelated API changes.

This is forward-looking. We are **not** restoring `net9.0` now — it is already gone from
4.x and the ecosystem has moved on. The rule applies to the next framework bump: when
.NET 11 support is added, `net10.0` stays alongside it unless dropping it is explicitly
decided. (Note that the longer a library ships single-TFM, the less free re-adding one
becomes: the code has already drifted onto .NET 10-only overload resolution in
`RetryPolicyBuilder` and `FluentRetryPolicyBuilder`.)

**4. Breaking anyway requires all four of these. This is what "unless explicitly stated" means.**

A break is a legitimate outcome; an *undeclared* break is not. To ship one:

* a MAJOR version bump, per `docs/RELEASING.md`;
* an ADR recording what breaks and why the compatible alternative was rejected;
* an entry in `docs/UPGRADING.md` telling consumers what to change;
* explicit acknowledgement in the PR description.

**5. Deprecate before removing.**

Mark the member `[Obsolete]` with a message naming the replacement and pointing at
`docs/UPGRADING.md`, ship that in a MINOR, and remove no earlier than the next MAJOR —
the treatment `BaseService` and `BaseSecureService` already received.

### Consequences

* Good: `dotnet pack` now fails on a binary-breaking change against the last published
  version. The class of mistake that motivated this ADR cannot be merged by accident; the
  gate is mechanical, not a review checklist.
* Good: The gate sits at pack/release time — the moment the contract is actually
  published — so it fits the existing `release.ps1` flow without slowing ordinary builds.
* Good: Deliberate breaks stay possible but leave an auditable trail (major + ADR +
  `UPGRADING.md`) instead of surfacing in a consumer's production logs.
* Bad: Surfaces get uglier. Overload sets accrete, and multi-targeting means keeping the
  code compilable against the oldest supported framework.
* Bad: Package validation must resolve the baseline package from `CaucaNuget`, so that
  feed becomes a prerequisite for `dotnet pack`, not just for the push step.
* Bad: `PackageValidationBaselineVersion` is a second version number to maintain. It must
  be advanced to the just-published version after each release, and after an *intended*
  break it must be reset to the new baseline or the gate will keep failing on a break that
  was already approved.

## Implementation Plan

- [x] Task 1: Add `EnablePackageValidation` and `PackageValidationBaselineVersion`
      (baseline `4.1.0`, the last published tag) to `Cauca.ApiClient/Cauca.ApiClient.csproj`.
- [x] Task 2: Correct the semver table in `docs/RELEASING.md`, which lists "new optional
      parameter" as a backwards-compatible MINOR change. Reclassify it as MAJOR, keep
      "new overload" as MINOR, add the TFM rule, and document advancing the validation
      baseline as a release step.
- [x] Task 3: Verify — solution builds warning-free, full test suite green, and
      `dotnet pack` resolves the 4.1.0 baseline and runs API compatibility against it.
