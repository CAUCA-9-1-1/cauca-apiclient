# Centralized Status-Code To Typed-Exception Translation

* Status: accepted
* Date: 2026-07-07
* Deciders: Cauca.ApiClient maintainers
* Technical Story: Repository architecture baseline

## Context and Problem Statement

When a request fails, consumers need meaningful, catchable error types rather than raw `HttpResponseMessage` inspection scattered across call sites. Both the fluent and legacy stacks must produce the same error semantics, and the failure detail (request URI, status code, response body) must be preserved for diagnostics and for the secure-auth flows that key off specific status codes.

## Decision Drivers

* Consumers should catch typed exceptions that express the failure (forbidden, not found, bad parameter, …).
* Consistent error semantics across every verb and across both the fluent and legacy stacks.
* Preserve request URI, status code, and response body through the failure.

## Considered Options

* **Option A**: A single `RestResponseValidator` that translates status codes into typed exceptions at the execution boundary, fed by an `ApiHttpException` that carries the transport failure.
* **Option B**: Let each call site inspect the `HttpResponseMessage`/status code and decide how to fail.

## Decision Outcome

Chosen option: **Option A**, because one translation point produces uniform typed exceptions for all callers, keeps the mapping in a single place to evolve, and preserves the failure detail that both consumers and the secure-auth retry logic depend on.

### Consequences

* Good: Uniform typed exceptions (`BadParameterApiException`, `UnauthorizedApiException`, `ForbiddenApiException`, `NotFoundApiException`, `InternalErrorApiException`, `UnexpectedResultException`, …) regardless of verb or stack.
* Good: A single mapping to evolve as API error contracts change.
* Bad: Control flow is exception-based.
* Bad: The mapping must be kept in sync with the server-side error contracts it translates.

## Maintenance Invariants
<!-- Behaviors to preserve; this decision is implemented -->
- Keep `RestResponseValidator.ThrowExceptionForStatusCode` as the single translation point for both the fluent and legacy stacks.
- `ApiHttpException` carries `RequestUri`, `StatusCode`, and `ResponseBody` from the transport failure into translation.
- Each handled status code maps to its dedicated typed exception; unmapped failures surface as an unexpected-result exception.
- Auth-expiry signals drive the secure refresh/re-login flows; keep them consistent with [Reactive Token Lifecycle For Secure Services](2026-07-07-reactive-secure-service-token-lifecycle.md) and [Self-Authenticating External-System Delegating Handler With Single-Flight Re-Auth](2026-07-07-external-system-self-auth-handler.md).
