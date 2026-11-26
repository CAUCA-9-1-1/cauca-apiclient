# Migration from `BaseService` / `BaseSecureService`

The Flurl-based classes are `[Obsolete]` and will be removed in a future major version. Migrate one
client at a time: `AddClient` and `AddSecureClient` accept both constructor shapes, so migrated and
legacy clients coexist.

## Rules

1. Keep every endpoint's behaviour: same URL, query string, headers, body, request and response types.
2. Keep authentication behaviour for secure clients.
3. Change only the client being migrated; do not rename unrelated public types.
4. Order: plain clients first, secure clients once their authentication flow is tested.

## Step by step

1. Base class: `BaseService<T>` → `FluentBaseService<T>`, `BaseSecureService<T>` → `FluentBaseSecureService<T>`.
2. Constructor: keep or add `Func<HttpClient>`; base call
   `: base(configuration, policyBuilder, clientFactory, apiPrefix)` (plain) or
   `: base(configuration, clientFactory, apiPrefix)` (secure).
3. Replace each verb helper with a `Request(...)` chain (table below).
4. Move path pieces into `AppendSegment(s)`, one piece per segment (each value is escaped, so
   `AppendSegment("a/b")` sends `a%2Fb`), and query strings into `AddQueryParameter` /
   `WithQueryParameters`. Remove any `Uri.EscapeDataString` the legacy code applied to path values:
   the builder escapes them, and escaping twice turns `%20` into `%2520`. A query left in the route
   still works (it is merged), but the builder escapes values for you.
5. Replace Flurl-specific code (`IFlurlRequest`, `GenerateRequest` overrides,
   `CapturedMultipartContent`, `FlurlHttpException`) with the builder, `PostFileAsync` and the
   `ApiClientException` family.
6. Find callers outside the class. The legacy verb helpers (`GetAsync(url)`, `PostAsync(url, entity)`…)
   are **public**; `Request(...)` is protected. Code that called `client.GetAsync<T>("...")` from outside
   needs a named method on the client.
7. Register with `AddClient` / `AddSecureClient` and the concrete configuration
   (see [registration-and-resilience.md](registration-and-resilience.md)).

## Verb mapping

| Legacy | Fluent |
|---|---|
| `GetAsync<T>("orders/" + id)` | `Request("orders").AppendSegment(id).GetAsync<T>()` |
| `GetAsync<T>($"orders?page={page}")` | `Request("orders").AddQueryParameter("page", page).GetAsync<T>()` |
| `GetAsync(url)`, `GetStringAsync(url)`, `GetBytesAsync(url)`, `GetStreamAsync(url)` | same names on the builder |
| `PostAsync<T>(url, entity)` / `PostAsync(url, entity)` | `Request(url).WithBody(entity).PostAsync<T>()` / `.PostAsync()` |
| `PutAsync<T>(url, entity)` | `Request(url).WithBody(entity).PutAsync<T>()` |
| `DeleteAsync(url)` / `DeleteAsync<T>(url)` | `Request(url).DeleteAsync()` / `.DeleteAsync<T>()` |
| `DeleteAsync(url, entity)` | `Request(url).WithBody(entity).DeleteAsync()` |
| `PostAndReceiveBytesAsync(url, entity)`, `PostAndReceiveStreamAsync(url, entity)` | `Request(url).WithBody(entity).PostAndReceiveBytesAsync()` / `...StreamAsync()` |
| `PostFileAsync(...)` (protected) | unchanged signatures on `FluentBaseService` |

Pass the caller's `CancellationToken` to the new call when the legacy one received it.

## Behaviour differences to check

| Topic | Legacy | Fluent |
|---|---|---|
| Query in the URL string | With a factory: sent as a query. Without: `?` encoded as `%3F` into the path | Kept and merged with the builder's parameters (dropped in 4.2.0) |
| `apiPrefix` | Ignored when no factory was passed | Always applied: the URL can change |
| Path pieces | Concatenated strings | One escaped value per segment, outer `/` trimmed: a `/` inside a value becomes `%2F`; `.` and `..` throw `ArgumentException` |
| JSON body | Flurl 4 serializer: **PascalCase** names (no naming policy) | camelCase (web defaults): harmless for ASP.NET Core servers, check any other server |
| `GetAsync<byte[]>` / `PostAsync<byte[]>` | JSON (base64 string) | raw bytes |
| `PutAsync<string>`, `DeleteAsync<string>`, `PostFileAsync<string>` | JSON-decoded string | raw text, quotes included |
| `ApiClientException.GetResponseAsync<T>()` | Flurl's JSON reader | `JsonSerializer.Deserialize<T>` without options (case-sensitive) |
| Unmapped status codes (409, 422, 429, 503…) | `NoResponseApiException` | `UnexpectedStatusCodeApiException` (with `StatusCode`): a `catch (NoResponseApiException)` no longer matches, `catch (ApiClientException)` does |
| Void call on a secure client after a token refresh | Threw `UnauthorizedApiException` even when the resend succeeded | Returns normally |
| File upload | Not retried | Retried like any call |
| `HttpClient` without a factory | Managed by Flurl | `new HttpClient()` per request: always inject `Func<HttpClient>` |

## Validation

For each migrated client:
- a test per method asserting the method, URL, query string, headers and body sent (mocked handler);
- for secure clients, tests for first login, `Token-Expired` refresh, and `Refresh-Token-Expired`
  re-login;
- a DI test that resolves the client from the real registration;
- the build has no new warning, and no `#pragma warning disable CS0618` remains for this client.
