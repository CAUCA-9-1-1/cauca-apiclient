# Secure clients

`FluentBaseSecureService<T>` calls an API protected by Cause.SecurityManagement. It logs in, adds
`Authorization: {type} {accessToken}` to every request and renews the token by itself.

## Flow

1. Before a request, when the instance has no access token: **login**.
2. On a 401 carrying the `Token-Expired` header: **refresh**, then resend the request once.
3. When the refresh answers 401 with `Refresh-Token-Expired` or `Token-Invalid`: **login** again.
4. Any other 401 → `UnauthorizedApiException`. A failure of the **resent** request is mapped like
   any other (`NotFoundApiException`, `UnexpectedStatusCodeApiException`…).

Each phase (login, request, refresh, re-login, resent request) runs with its own full retry budget.

| Step | User login (default) | External-system login (`UseExternalSystemLogin = true`) |
|---|---|---|
| Login | `POST {auth}/{prefix}/Authentication/logon`, body `{ userId, password }` | `POST {auth}/{prefix}/Authentication/logonforexternalsystem`, body `{ apiKey }` (from `UserId`) |
| Refresh | `POST {auth}/{prefix}/Authentication/refresh` | `POST {auth}/{prefix}/Authentication/refreshforexternalsystem` |

`{auth}` is `ApiBaseUrlForAuthentication`, or `ApiBaseUrl` when it is `null`. The `apiPrefix` applies
to these URLs too: a client whose API lives under `api/` reaches `.../api/Authentication/logon`, which
matches the routes of Cause.SecurityManagement's `AuthenticationController` and the legacy aliases of
its `ExternalSystemAuthenticationController`.

Login errors:
- 401 → `InvalidCredentialException` (derives from `Exception`, not `ApiClientException`). Its message
  contains the `UserId` for a user login.
- no response → `NoResponseApiException`; any other failure → `InternalErrorApiException`.

A refresh failure other than an expired or invalid refresh token (no response, 500…) is swallowed and
leaves the access token empty. The current request is still resent, with an empty token, and usually
fails with an unmapped 401; only the next call logs in again.

## State and concurrency

The tokens live in the public `AccessInformation` field of the instance. The class does not
synchronise them: two concurrent requests on one instance with an expired token both refresh. Keep
one instance per scope or unit of work (see the lifetimes in
[registration-and-resilience.md](registration-and-resilience.md)). Do not log `AccessInformation`.

## A plain `HttpClient` with external-system authentication

For code that uses `HttpClient` directly (generated clients, Refit, a third-party SDK) against a
Cauca API, add the authentication as a message handler:

```csharp
services.AddHttpClient<IOrdersApi, OrdersApi>(client => client.BaseAddress = new Uri(settings.ApiBaseUrl))
    .AddCaucaExternalSystemAuth(settings, apiPrefix: "api");
```

`AddCaucaExternalSystemAuth(IConfiguration configuration, string apiPrefix = null)` adds a
`DelegatingHandler` that:
- logs in on first use, through its own named client `CaucaExternalSystemAuth:{name}`;
- serialises login and refresh behind a semaphore, so concurrent requests refresh once;
- on 401 `Token-Expired` refreshes, on `Refresh-Token-Expired` or `Token-Invalid` logs in again, then
  resends the request once (the body is buffered for that);
- uses the login body and URLs of the table above according to `UseExternalSystemLogin`. Set it to
  `true` for an external system: the name of the method does not do it for you.

`IHttpClientFactory` recycles handlers (every 2 minutes by default), so the token is lost and a new
login happens at each rotation.

Timeouts and errors with this handler:
- login and refresh use the passed configuration's `RequestTimeoutInSeconds` (at `0`, every login
  fails with `NoResponseApiException` after about 14 s of retries);
- the API call itself is bounded only by the typed client's own `HttpClient.Timeout` (100 s by default);
- login and refresh failures come out of `SendAsync` as exceptions (`InvalidCredentialException`,
  `NoResponseApiException`, `InternalErrorApiException`), not as HTTP responses.
