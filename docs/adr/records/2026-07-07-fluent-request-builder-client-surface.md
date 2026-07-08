# Fluent Request-Builder As The Primary Client Surface

* Status: accepted
* Date: 2026-07-07
* Deciders: Cauca.ApiClient maintainers
* Technical Story: Repository architecture baseline

## Context and Problem Statement

The library exposes abstract base services that consumers derive from to call REST APIs. Historically, every HTTP verb had a dedicated helper (`GetAsync<T>(url)`, `PostAsync<T>(url, entity)`, …) where route, query parameters, headers, and bodies were passed as loose method parameters. Route composition and request shaping were implicit at the call site, which made calls hard to read, hard to extend with new concerns (query groups, multipart, headers), and inconsistent across verbs.

## Decision Drivers

* Readable, self-documenting call sites that express route, query, headers, and body explicitly.
* A single execution pipeline where retry, error translation, and authentication can be applied uniformly.
* Composable path building (segments), query grouping from objects, and multipart support.
* Consistency across all verbs and testability of the request definition.

## Considered Options

* **Option A**: Introduce a fluent request builder — `Request("route").AppendSegment(...).WithQueryParameters(...).WithBody(...).PostAsync<T>()` — backed by an internal `IFluentRequestExecutor`, exposed by `FluentBaseService<TConfiguration>` as the primary surface.
* **Option B**: Keep growing the verb-helper methods with additional overloads for headers, query groups, and multipart.

## Decision Outcome

Chosen option: **Option A**, because the builder centralizes all request shaping in `FluentRequestDefinition` and routes every verb through one `IFluentRequestExecutor.SendAsync` pipeline, giving a single choke point for retry, timeout, error translation, and auth while keeping call sites declarative.

### Consequences

* Good: Call sites read as intent; route semantics are explicit through `AppendSegment`/`AppendSegments`.
* Good: One execution path means retry, per-request timeout, and error translation are applied consistently to every verb.
* Bad: During migration the library carries two surfaces (fluent and legacy verb helpers).
* Bad: `FluentRequestBuilder` and the executor are coupled through an internal interface, so terminal-verb changes touch both.

## Maintenance Invariants
<!-- Behaviors to preserve; this decision is implemented -->
- Keep the `Request(route)` builder methods stable: `AppendSegment` / `AppendSegments`, `AddQueryParameter` / `WithQueryParameters`, `WithHeader` / `WithHeaders`, `WithBody`, and internal `WithMultipart`.
- Route every terminal verb (`GetAsync`, `PostAsync`, `PutAsync`, `DeleteAsync`, and their typed/stream/byte variants) through `IFluentRequestExecutor.SendAsync`.
- Preserve request-shaping guards: GET requests with a body throw, and a request cannot define both a JSON body and multipart content.
- New client code derives from `FluentBaseService` / `FluentBaseSecureService`, never the legacy base classes (see [Deprecate Legacy Verb-Helper Base Services In Favour Of The Fluent API](2026-07-07-deprecate-legacy-verb-helper-base-services.md)).
