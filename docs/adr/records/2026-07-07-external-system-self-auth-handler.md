# Self-Authenticating External-System Delegating Handler With Single-Flight Re-Auth

* Status: accepted
* Date: 2026-07-07
* Deciders: Cauca.ApiClient maintainers
* Technical Story: #6 — ExternalSystem self-auth for generated/typed clients

## Context and Problem Statement

Typed clients generated against an external API (which do not derive from `FluentBaseSecureService`) still need external-system authentication. They are registered through `IHttpClientFactory`, so authentication has to live in the HTTP pipeline rather than in a base class. Under concurrency, many in-flight requests can observe a `401` at once; a naive handler would fire a login/refresh per request, stampeding the token endpoint.

## Decision Drivers

* Reuse external-system authentication for `IHttpClientFactory` typed and generated clients.
* Avoid a thundering herd of duplicate login/refresh calls when concurrent requests hit `401` together.
* Correctly distinguish an expired access token (refresh) from an expired or invalid refresh token (full re-login).
* Replay the original request, including its body, after re-authentication.

## Considered Options

* **Option A**: A `DelegatingHandler` (`CaucaExternalSystemAuthHandler`) that ensures login, attaches the token, and on `401` re-authenticates behind a single-flight `SemaphoreSlim` gate guarded by a token-equality check, then resends a cloned request.
* **Option B**: Authenticate per call inside each generated client, or attach a static token with no in-pipeline recovery.

## Decision Outcome

Chosen option: **Option A**, because a delegating handler makes authentication transparent to generated clients, and the single-flight gate with a token-comparison guard ensures only the first waiter re-authenticates while the others reuse the freshly obtained token.

### Consequences

* Good: Authentication is transparent to typed/generated clients registered via `AddCaucaExternalSystemAuth`.
* Good: The token-equality guard inside the gate means concurrent `401`s trigger exactly one re-auth, not one per request.
* Good: Access-token expiry refreshes; refresh-token expiry or invalidity forces a full re-login.
* Bad: The handler holds mutable token state, so it must be scoped to its own named auth client, not shared globally.
* Bad: Request content is buffered and cloned to allow a resend, adding memory cost for large bodies.

## Maintenance Invariants
<!-- Behaviors to preserve; this decision is implemented -->
- Keep re-authentication single-flight: acquire `_loginGate`, and re-authenticate only when `AccessInformation.AccessToken` still equals the token that was used for the failed request.
- Distinguish failure modes: `AccessTokenIsExpired()` → `RefreshToken`; `RefreshTokenIsExpired()` or `RefreshTokenIsInvalid()` → `Login`.
- Buffer request content before the first send and clone the request (method, URI, headers, buffered body) before resending.
- Dispose `_loginGate` with the handler.
- This handler coordinates with the secure-service token lifecycle in [Reactive Token Lifecycle For Secure Services](2026-07-07-reactive-secure-service-token-lifecycle.md); both rely on the same status-code auth signals.
