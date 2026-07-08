# Drop Flurl For System.Net.Http In The Fluent Stack

* Status: accepted
* Date: 2026-07-07
* Deciders: Cauca.ApiClient maintainers
* Technical Story: Repository architecture baseline

## Context and Problem Statement

The legacy base services are built on `Flurl.Http`: request construction, sending, and response deserialization all go through Flurl abstractions. The fluent stack was an opportunity to reconsider that dependency. Flurl adds a third-party layer over `HttpClient`, its own request/response model, and transitive dependencies, while the platform now offers `IHttpClientFactory`, `HttpRequestMessage`, and `System.Net.Http.Json` directly.

## Decision Drivers

* Reduce the third-party dependency surface for consumers of the modern stack.
* Direct control over `HttpRequestMessage`, completion options, per-request timeout, and serialization.
* Alignment with `IHttpClientFactory` and platform JSON (`System.Text.Json`).

## Considered Options

* **Option A**: Build the fluent stack directly on `System.Net.Http` / `System.Net.Http.Json`, keeping Flurl only in the obsolete legacy stack.
* **Option B**: Reimplement the fluent stack on top of Flurl as well, for consistency with the legacy code.

## Decision Outcome

Chosen option: **Option A**, because building on the platform HTTP primitives removes an abstraction layer, gives the library full control over the request lifecycle, and lets the fluent stack shed the Flurl dependency once the legacy stack is retired.

### Consequences

* Good: The fluent execution path has no Flurl dependency; requests are plain `HttpRequestMessage` with `HttpCompletionOption.ResponseHeadersRead`.
* Good: Serialization uses `JsonSerializerDefaults.Web` consistently, controlled by the library.
* Bad: Conveniences Flurl provided (multipart, typed response reads) are reimplemented in-house (`MultipartFormDataBuilder`, `ReadValueAsync`).
* Bad: The `Flurl.Http` package reference remains until the legacy stack is removed.

## Maintenance Invariants
<!-- Behaviors to preserve; this decision is implemented -->
- The fluent stack (`FluentBaseService`, `FluentRequestBuilder`, `FluentRequestDefinition`, handlers) must not take a Flurl dependency; use `System.Net.Http` and `System.Net.Http.Json`.
- Keep `System.Text.Json` `Web` defaults for request/response serialization.
- Confine `Flurl.Http` usage to the obsolete legacy classes; when they are removed, drop the `Flurl.Http` `PackageReference` from `Cauca.ApiClient.csproj`.
