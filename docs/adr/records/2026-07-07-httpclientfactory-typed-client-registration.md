# IHttpClientFactory-Based Typed Client Registration

* Status: accepted
* Date: 2026-07-07
* Deciders: Cauca.ApiClient maintainers
* Technical Story: Repository architecture baseline

## Context and Problem Statement

Consumers register their client services in DI. Clients need a properly pooled `HttpClient` (correct `HttpMessageHandler` lifetime to avoid socket exhaustion and stale DNS), a per-request timeout controlled by the library rather than the ambient `HttpClient.Timeout`, and safe service lifetimes given that secure clients carry mutable token state.

## Decision Drivers

* Correct `HttpMessageHandler` lifetime and pooling through `IHttpClientFactory`.
* Per-request timeout enforced by the library (secure/reactive flows need this), not a fixed handler timeout.
* Prevent unsafe lifetimes for clients that hold mutable authentication state.
* Let clients opt into a factory-supplied `HttpClient` without forcing a constructor signature on those that do not need one.

## Considered Options

* **Option A**: `AddClient<T>` / `AddSecureClient<T>` register a named `HttpClient` via `IHttpClientFactory` and construct the client with `ActivatorUtilities`, injecting a `Func<HttpClient>` only when the constructor accepts one; the named client's timeout is set to `InfiniteTimeSpan` and per-request timeout is enforced inside the service.
* **Option B**: Construct `new HttpClient()` per service (socket exhaustion, no pooling) or inject a single shared `HttpClient` directly.

## Decision Outcome

Chosen option: **Option A**, because factory-managed named clients give correct handler lifetimes, the `InfiniteTimeSpan` handler timeout hands timeout control to the library's per-request linked `CancellationTokenSource`, and constructor detection keeps registration flexible.

### Consequences

* Good: Pooled, correctly-rotated handlers; per-request timeout owned by `FluentBaseService` (`Configuration.RequestTimeoutInSeconds`).
* Good: `AddCaucaExternalSystemAuth` composes on the same model, registering its own auth-named client plus the delegating handler.
* Bad: Constructor inspection via reflection determines whether to pass the `Func<HttpClient>` argument.
* Bad: A guard is required to forbid singleton secure clients.

## Maintenance Invariants
<!-- Behaviors to preserve; this decision is implemented -->
- `AddSecureClient<T>` throws when registered as `Singleton`, because secure clients keep per-instance mutable access-token state (see [Reactive Token Lifecycle For Secure Services](2026-07-07-reactive-secure-service-token-lifecycle.md)).
- Named `HttpClient` timeout stays `Timeout.InfiniteTimeSpan`; the effective timeout is applied per request by the service via a linked `CancellationTokenSource`.
- The resolved client name defaults to the client type's `FullName`; a `Func<HttpClient>` is injected only when a public constructor accepts one.
- `AddCaucaExternalSystemAuth` registers a dedicated `CaucaExternalSystemAuth:{name}` client and attaches `CaucaExternalSystemAuthHandler`.
