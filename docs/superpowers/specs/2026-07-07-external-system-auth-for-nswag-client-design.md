# ExternalSystem Auth for NSwag-Generated Clients — Design

**Date:** 2026-07-07
**Status:** Approved design, ready for implementation planning
**Repos affected:** `cauca-apiclient` (reusable surface) and `transfert-api` (generated client wiring)

## Problem

`Cauca.SurviRaoTransfer` publishes a NuGet client for its internal API, generated
with NSwag during CI. That generated client is a bare typed-`HttpClient` wrapper —
it does no authentication. Consumers of the package would each have to reimplement
ExternalSystem login (API-key logon) and token refresh by hand.

`Cauca.ApiClient` already implements the exact ExternalSystem login + refresh flow,
but only reachable through `FluentBaseSecureService` and its fluent request builder.
The logic is locked inside `internal sealed FluentRefreshTokenHandler` and cannot
drive a foreign HTTP client such as an NSwag-generated one.

## Goal

Make the generated client **authenticate itself**. A consumer references the client
package, supplies a base URL and an API key, and every call transparently performs
ExternalSystem login and token refresh — with **behavior identical** to
`Cauca.ApiClient` (no drift over time).

### Success criteria

- Consumer wiring is a single call: `services.AddSurviRaoTransferInternalClient(baseUrl, apiKey)`.
- Login + refresh + re-login-on-expiry happen inside the client's HTTP pipeline,
  invisible to the consumer.
- The auth behavior is the *same code* as `Cauca.ApiClient` (referenced, not copied).
- Existing `FluentBaseSecureService` clients keep working unchanged.
- The NSwag-generated `.cs` file is never hand-edited; regeneration is safe.

## Constraints and decisions

| Decision | Choice | Rationale |
|---|---|---|
| Where auth code lives | **Reference `Cauca.ApiClient`** from the generated client (Option A) | Guaranteed parity — literally the same code. Fast to build. Accepts transitive Flurl (until the DroppingFlurl branch lands) + Polly. |
| Integration seam | **`DelegatingHandler`** on the typed client's pipeline | Idiomatic HttpClientFactory. Zero edits to generated code. Supports refresh-and-retry, which NSwag `ProcessResponse` partials cannot. |
| Token lifetime | **Lazy per handler, in-memory only, refresh on 401** | Short-lived usage; token never persisted. Avoids a login round-trip on every call while keeping the token in memory briefly. |
| Consumer inputs | **Base URL + API key only** | The registration builds the `Cauca.ApiClient` configuration internally. |

### Prerequisite (confirmed)

The SurviRao Transfer internal API implements the ExternalSystem auth contract:
it exposes `…/Authentication/logonforexternalsystem` and
`…/Authentication/refreshforexternalsystem`, and signals token state on a 401 via the
`Token-Expired`, `Refresh-Token-Expired`, and `Token-Invalid` response headers.

## ExternalSystem login flow (reference)

Already implemented in `Cauca.ApiClient`; the new handler must reproduce it exactly.

- **Login** — `POST {authBaseUrl}/{apiPrefix}/Authentication/logonforexternalsystem`
  with body `{ ApiKey }` (the API key is carried in `IConfiguration.UserId`, with
  `UseExternalSystemLogin = true`). Response: `LoginResult { AuthorizationType, AccessToken, RefreshToken }`.
- **Refresh** — `POST …/Authentication/refreshforexternalsystem` with body
  `{ AccessToken, RefreshToken }`. Response: `TokenRefreshResult { AccessToken }`.
- **Authorization header** — `Authorization: {AuthorizationType} {AccessToken}`
  (typically `Bearer`).
- **Expiry signalling (on HTTP 401):**
  - `Token-Expired` → access token expired → refresh, then retry.
  - `Refresh-Token-Expired` / `Token-Invalid` → refresh token gone → re-login, then retry.

## Architecture

### Component 1 — `Cauca.ApiClient` reusable surface

**`ExternalSystemAuthenticator`** (new)
- The login/refresh core, lifted out of `internal sealed FluentRefreshTokenHandler`.
- Constructed from an `IConfiguration` (base URL, API key, timeout) and a
  `Func<HttpClient>` used to make the login/refresh calls.
- Exposes: `Login`, `Refresh`, and access to the current `AccessInformation` (token +
  authorization type).
- Same endpoints, same request bodies, same `LoginResult` / `TokenRefreshResult`
  deserialization, same exception mapping as today.
- `FluentRefreshTokenHandler` is refactored to a **thin wrapper** delegating to
  `ExternalSystemAuthenticator`, so existing secure clients get identical behavior
  from the same code path. **No public behavior change.**

**`CaucaExternalSystemAuthHandler : DelegatingHandler`** (new, public)
- Holds a short-lived `AccessInformation` for the handler's own lifetime; nothing
  persisted beyond memory.
- `SendAsync`:
  1. If no access token yet, `Login`.
  2. Buffer the request content (so a retry can re-send the same body), set the
     `Authorization` header, send.
  3. On a 401, inspect response headers:
     - `Token-Expired` → `Refresh`, reattach header, resend once.
     - `Refresh-Token-Expired` / `Token-Invalid` → `Login`, reattach header, resend once.
     - otherwise → return the 401 as-is.
- Uses a **separate plain `HttpClient`** (via `Func<HttpClient>`) for its own
  login/refresh calls so it never recurses through its own handler.
- Single-flight guard so concurrent requests through one handler don't trigger
  duplicate logins.

**`AddCaucaExternalSystemAuth(...)`** (new, public — `ServiceCollectionExtensions`)
- Registers `CaucaExternalSystemAuthHandler` as a transient message handler and a
  named plain `HttpClient` for the authenticator's login/refresh calls.
- Chainable onto any `IHttpClientBuilder`.

### Component 2 — generated client package (`transfert-api`)

**`InternalClientRegistration.cs`** (new, hand-written, checked in)
- NSwag only rewrites `SurviRaoTransferInternalClient.cs` (per `nswag.internal-client.json`
  `output`), so this sibling file survives regeneration and compiles via the SDK glob.
- Exposes:
  ```csharp
  public static IServiceCollection AddSurviRaoTransferInternalClient(
      this IServiceCollection services, string baseUrl, string apiKey)
  ```
  It builds the `Cauca.ApiClient` configuration
  (`UseExternalSystemLogin = true`, `UserId = apiKey`, base URL + auth base URL = `baseUrl`),
  registers `AddHttpClient<ISurviRaoTransferInternalClient, SurviRaoTransferInternalClient>()`
  (setting `BaseUrl`), and chains `.AddCaucaExternalSystemAuth(...)`.

**`Cauca.SurviRaoTransfer.InternalClient.csproj`** (edited once)
- Add `<PackageReference Include="Cauca.ApiClient" Version="…" />`. The csproj is
  checked in and not regenerated, so this persists.

## Data flow (consumer perspective)

```
Consumer:  services.AddSurviRaoTransferInternalClient(baseUrl, apiKey);
           var client = provider.GetRequiredService<ISurviRaoTransferInternalClient>();
           await client.SomeOperationAsync();

Pipeline:  SurviRaoTransferInternalClient
             → HttpClient (typed)
               → CaucaExternalSystemAuthHandler   ← login/refresh here
                 → primary handler → network
```

The consumer never sees a token, a handler, or a Cauca configuration object.

## Error handling

Reuses `Cauca.ApiClient` exception semantics from the shared authenticator:

- Invalid API key on login → `InvalidCredentialException`.
- No response from the auth endpoint → `NoResponseApiException`.
- Other login failures → `InternalErrorApiException`.
- Downstream (business) responses and NSwag `ApiException`s pass through untouched —
  the handler only intervenes on 401s carrying the ExternalSystem expiry headers.

## Testing

Handler-level tests (stub `HttpMessageHandler`), in `Cauca.ApiClient.Tests`:

1. First call with no token → performs login, then the business request succeeds.
2. 401 + `Token-Expired` → refreshes, retries once, succeeds.
3. 401 + `Refresh-Token-Expired` (and `Token-Invalid`) → re-logs-in, retries once, succeeds.
4. Request body is preserved across the retry (content buffering).
5. Login/refresh requests target the correct `logonforexternalsystem` /
   `refreshforexternalsystem` paths with the `{ ApiKey }` / `{ AccessToken, RefreshToken }`
   bodies.
6. A non-401 downstream failure is returned unchanged (handler does not interfere).
7. Concurrent requests through one handler perform a single login (single-flight).

Regression:

8. Existing `FluentBaseSecureService` / `FluentRefreshTokenHandler` tests still pass
   after the `ExternalSystemAuthenticator` extraction (behavior unchanged).

## Out of scope

- Password-based (non-ExternalSystem) login for generated clients.
- Persisting or sharing tokens across process restarts or across handlers.
- Changing the NSwag generation configuration or the OpenAPI contract.
- Auth work inside the SurviRao Transfer internal API itself (already implements the contract).

## Open implementation details (for the plan)

- Exact `apiPrefix` / auth base-URL composition for the internal API (`RequestUriBuilder`
  builds `{authBaseUrl}/{apiPrefix}/Authentication/…`). Verify the internal API's real
  auth route and set the configuration accordingly.
- Whether `AddCaucaExternalSystemAuth` should accept a prebuilt `IConfiguration` or the
  raw `baseUrl` + `apiKey` (the generated wrapper can adapt either way).
- The `Cauca.ApiClient` package version to pin in the generated client's csproj.
