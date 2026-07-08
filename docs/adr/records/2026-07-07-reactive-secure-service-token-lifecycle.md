# Reactive Token Lifecycle For Secure Services

* Status: accepted
* Date: 2026-07-07
* Deciders: Cauca.ApiClient maintainers
* Technical Story: Repository architecture baseline

## Context and Problem Statement

`FluentBaseSecureService<TConfiguration>` calls APIs that require a bearer token. It must obtain a token, attach it to every request, and recover when the access token expires mid-session — without pushing token management onto the consumer. The service holds mutable `AccessInformation` (access token, refresh token, authorization type) per instance.

## Decision Drivers

* Transparent authentication: consumers call business methods, not login/refresh plumbing.
* Minimize token endpoint traffic — only log in when there is no token, only refresh when the current token is rejected.
* Recover from access-token expiry that happens during a live session.

## Considered Options

* **Option A**: Reactive lifecycle inside `ExecuteAsync` — lazy login when no token is present, then on a request that fails with an expired-access-token response, refresh once and retry the original request.
* **Option B**: Require callers to explicitly log in and refresh, or proactively refresh on a timer before every call.

## Decision Outcome

Chosen option: **Option A**, because reactive lazy login plus refresh-then-retry keeps authentication invisible to consumers, avoids unnecessary token calls, and recovers from expiry without proactive scheduling.

### Consequences

* Good: Consumers derive from `FluentBaseSecureService` and get login/refresh for free; every request carries an `Authorization` header.
* Good: Token endpoints are hit only on first use and on actual expiry.
* Bad: `AccessInformation` is mutable per-instance state, so an instance is not safe to share across concurrent unrelated callers (drives the no-singleton rule in [IHttpClientFactory-Based Typed Client Registration](2026-07-07-httpclientfactory-typed-client-registration.md)).
* Bad: The reactive path retries once after a refresh; a persistently failing refresh surfaces as a translated exception.

## Maintenance Invariants
<!-- Behaviors to preserve; this decision is implemented -->
- `LoginWhenLoggedOut` runs before the request when `AccessInformation.AccessToken` is empty.
- Refresh-then-retry triggers only when the failure is an expired access token (`ApiHttpException.AccessTokenIsExpired()`); other failures go straight to status-code translation.
- Secure requests attach `Authorization` via `GetAuthorizationHeaderValue()` (`{AuthorizationType} {AccessToken}`).
- Secure service instances are never registered or shared as singletons because of their mutable token state.
