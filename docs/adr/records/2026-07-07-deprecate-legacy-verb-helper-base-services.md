# Deprecate Legacy Verb-Helper Base Services In Favour Of The Fluent API

* Status: accepted
* Date: 2026-07-07
* Deciders: Cauca.ApiClient maintainers
* Technical Story: Repository architecture baseline

## Context and Problem Statement

`BaseService<TConfiguration>` and `BaseSecureService<TConfiguration>` are the original client surface. With the fluent request builder now the primary surface (see [Fluent Request-Builder As The Primary Client Surface](2026-07-07-fluent-request-builder-client-surface.md)), the repository needs a way to steer new development toward the fluent API without breaking the existing consumers that still derive from the legacy classes.

## Decision Drivers

* Guide new code to the fluent API through a compile-time signal.
* Avoid a hard breaking change for consumers already depending on the legacy base classes.
* Provide an explicit, documented migration path.

## Considered Options

* **Option A**: Soft-deprecate — mark the legacy base classes `[Obsolete]` with a message pointing to `docs/UPGRADING.md`, keep them compiling, and document migration rules.
* **Option B**: Remove the legacy base classes outright and force all consumers onto the fluent API immediately.

## Decision Outcome

Chosen option: **Option A**, because a soft deprecation gives consumers a compiler warning and a migration document while preserving source compatibility, letting teams migrate incrementally instead of on a forced schedule.

### Consequences

* Good: Consumers see an `[Obsolete]` warning at every legacy derivation and are pointed to `UPGRADING.md`.
* Good: No forced break; existing services keep compiling.
* Bad: Two client stacks must be maintained during the deprecation window.
* Bad: The legacy stack keeps the Flurl dependency alive (see [Drop Flurl For System.Net.Http In The Fluent Stack](2026-07-07-drop-flurl-for-system-net-http.md)).

## Maintenance Invariants
<!-- Behaviors to preserve; this decision is implemented -->
- Keep the `[Obsolete]` attributes on `BaseService<TConfiguration>` and `BaseSecureService<TConfiguration>` with messages directing consumers to `docs/UPGRADING.md`.
- Do not add new features to the legacy base classes; new capabilities land on the fluent stack only.
- Keep `docs/UPGRADING.md` authoritative for the legacy-to-fluent migration mapping.
- Removal of the legacy classes is a breaking change and requires its own ADR plus a MAJOR release.
