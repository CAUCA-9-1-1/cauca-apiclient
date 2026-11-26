---
name: cauca-apiclient
description: >
  Cauca.ApiClient, the Cauca library for typed HTTP clients between Cauca services (net10.0 and
  net11.0): FluentBaseService / FluentBaseSecureService and the Request(...) builder, automatic
  login and token refresh against Cause.SecurityManagement APIs, AddClient / AddSecureClient /
  AddCaucaExternalSystemAuth registration, retries, timeouts, exception mapping, and migration from
  the obsolete Flurl-based BaseService / BaseSecureService. Use it whenever code references
  Cauca.ApiClient, inherits one of its base classes, or when someone asks to write, fix, migrate or
  register a client that calls another Cauca API — even if the library is not named. Triggers:
  "client API", "appeler l'API de", "migrer vers FluentBaseService", "BaseSecureService obsolète",
  "AddSecureClient", "Flurl", "NotFoundApiException", "Cauca.ApiClient".
---

# Cauca.ApiClient

Package `Cauca.ApiClient` 4.2.0, `net10.0` and `net11.0`. Source: the `cauca-apiclient` repository
(GitHub `CAUCA-9-1-1`), project `Cauca.ApiClient/`. Every fact below was checked against the 4.2.0
source on 2026-11-26. The repository's `docs/UPGRADING.md` is the migration guide. Where it disagrees
with this skill, the source wins.

New clients inherit `FluentBaseService<TConfiguration>` or `FluentBaseSecureService<TConfiguration>`
(namespace `Cauca.ApiClient.Services`). `BaseService` and `BaseSecureService` (Flurl) are `[Obsolete]`
and will be removed in a future major version.

Example in production: `caucaappsv2`, `CaucaAppsV2/Cause.CAD.Fire.Domain/WebApi/SurviRaoClient.cs` (a
plain client). It passes no `CancellationToken` and relies on the case-sensitive `GetResponseAsync<T>()`:
do not copy those two points.

```csharp
public class OrdersClient(OrdersConfiguration configuration, Func<HttpClient> clientFactory)
    : FluentBaseSecureService<OrdersConfiguration>(configuration, clientFactory, apiPrefix: "api")
{
    public Task<OrderDto> GetOrderAsync(Guid orderId, CancellationToken cancellationToken)
        => Request("orders").AppendSegment(orderId).GetAsync<OrderDto>(cancellationToken);
}

services.AddSingleton(new OrdersConfiguration { ApiBaseUrl = "...", RequestTimeoutInSeconds = 30, /* ... */ });
services.AddSecureClient<OrdersClient>(ServiceLifetime.Scoped);
```

## Where to read next

| Task | Read |
|---|---|
| Write requests: segments, query, headers, body, files, response types, errors | [references/fluent-requests.md](references/fluent-requests.md) |
| Register a client, configuration, timeouts, retries, lifetimes | [references/registration-and-resilience.md](references/registration-and-resilience.md) |
| Call a secured API: user or external-system login, refresh, `AddCaucaExternalSystemAuth` | [references/secure-clients.md](references/secure-clients.md) |
| Migrate a `BaseService` / `BaseSecureService` client | [references/migration-from-legacy.md](references/migration-from-legacy.md) |

## Facts that bite

**Configuration**
- `IConfiguration.RequestTimeoutInSeconds` defaults to `0` in any class that does not set it, and `0`
  cancels every request at once: each call fails with `NoResponseApiException` after the retries.
  Always set it.
- That timeout stops at the response headers. Reading the body has no timeout besides the caller's
  `CancellationToken`: pass one with a deadline.
- The constructor takes the concrete configuration type. `services.Configure<T>()` alone registers
  only `IOptions<T>`, so the client fails to resolve. Register the concrete instance.

**Construction**
- Take `Func<HttpClient>` in the constructor and pass it to the base. Without it, the base creates a
  `new HttpClient()` **for every request** (socket exhaustion), and `AddClient` cannot inject the
  factory.
- `FluentBaseSecureService` has two constructors: `(configuration, policyBuilder)` and
  `(configuration, clientFactory, apiPrefix)`. You cannot pass both a factory and a custom retry
  policy builder.

**Requests**
- The route given to `Request(...)` must not contain a query string: the builder rebuilds the query
  from its own parameters and **silently drops** `?a=1` written in the route.
- Segments are not escaped by the builder. A `/`, `?` or `#` inside a value changes the URL. Escape
  free-text values with `Uri.EscapeDataString`.
- Query values go through `Convert.ToString(value, InvariantCulture)`. Collections are not expanded
  (`ids=System.Collections.Generic.List...`), one key holds one value, and a `DateTime` comes out as
  `MM/dd/yyyy HH:mm:ss`. Pre-format dates (`"O"`) and avoid collection parameters.
- `PutAsync` and `PatchAsync` exist only as generic `PutAsync<T>()` / `PatchAsync<T>()`. With a JSON
  `T`, a 204 No Content throws `JsonException` after the server applied the change: use `<string>`.
- `Content-Type` cannot be set: `WithHeader` drops content headers and bodies are always
  `application/json`.

**Errors and retries**
- 400, 401, 403, 404 and 500 become `BadParameterApiException`, `UnauthorizedApiException`,
  `ForbiddenApiException`, `NotFoundApiException` and `InternalErrorApiException`; no response becomes
  `NoResponseApiException`. Any **other** status (409, 422, 429, 503 after retries…) is thrown as the
  internal `ApiHttpException`, which callers cannot catch by type: catch `Exception` at the boundary.
  On a secure client, a request resent after a token refresh is not mapped at all.
- The default policy retries 408, 502, 503, 504 and timeouts 3 times (2 s, 4 s, 8 s) for **every**
  verb, POST included. A non-idempotent call can run twice on the server.
- `ApiClientException.GetResponseAsync<T>()` deserializes the error body with default
  `JsonSerializer` options: property names are **case-sensitive**, unlike the client's own reads.

**Secure clients**
- The token lives in the client instance. `AddSecureClient` refuses `Singleton`; with the default
  `Transient` lifetime, every resolved instance logs in again. Prefer `Scoped` in a web API and one
  instance per unit of work in a worker.

## Before production

These clients carry calls between 9-1-1 systems. Before a new or migrated client reaches production:
- compare the URLs, query strings, headers and bodies it sends with the previous version, in a test
  against a recorded or mocked server;
- test login, token expiry and refresh-token expiry against the real authentication API in a test
  environment;
- confirm the timeout and retry budget (up to 4 attempts × timeout + 14 s per phase; a secure client
  can chain login, request, refresh and resend) fits the caller's own deadline, and that retried POSTs
  are safe.
