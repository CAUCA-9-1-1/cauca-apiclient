# Registration and resilience

## Configuration

Implement `Cauca.ApiClient.Configuration.IConfiguration`:

| Property | Role |
|---|---|
| `ApiBaseUrl` | Base URL of every request |
| `ApiBaseUrlForAuthentication` | Base URL of login and refresh; `null` falls back to `ApiBaseUrl` |
| `UserId`, `Password` | Credentials of a user login; with `UseExternalSystemLogin`, `UserId` is the API key and `Password` is unused |
| `UseExternalSystemLogin` | Log in as an external system instead of a user (see [secure-clients.md](secure-clients.md)) |
| `RequestTimeoutInSeconds` | Time allowed for each attempt, from sending to the end of the body read. `0` cancels at once: always set it |

`Password` and an external-system API key are secrets. Bind them from the deployment secret store, not
from a committed `appsettings.json`.

## DI registration

`Cauca.ApiClient.Extensions.ServiceCollectionExtensions`:

```csharp
services.AddClient<TClient>(lifetime = ServiceLifetime.Transient, clientName = null);
services.AddSecureClient<TClient>(lifetime = ServiceLifetime.Transient, clientName = null); // throws on Singleton
```

Both register a named `HttpClient` (name: `clientName` or the client's full type name, timeout
infinite because the client applies its own) and build `TClient` with `ActivatorUtilities`. When a
public constructor has a `Func<HttpClient>` parameter, they pass a factory over
`IHttpClientFactory.CreateClient(name)`. Every other constructor argument, the configuration included,
comes from the container.

Register the **concrete** configuration:

```csharp
services.AddSingleton(configuration.GetSection("OrdersApi").Get<OrdersConfiguration>());
// or, when the application already uses IOptions<T>:
services.Configure<OrdersConfiguration>(configuration.GetSection("OrdersApi"));
services.AddSingleton(provider => provider.GetRequiredService<IOptions<OrdersConfiguration>>().Value);
```

Recommended for a client shipped in a library: one `Add<Name>Client` extension in that library, which
registers the client, maps its interface to the same instance and offers a configuration overload.
`docs/UPGRADING.md` of the repository has the full example (`AddSurviCartoClient`).

```csharp
services.AddClient<SurviCartoClient>(lifetime);
services.Add(new ServiceDescriptor(typeof(ISurviCartoClient),
    provider => provider.GetRequiredService<SurviCartoClient>(), lifetime));
```

To add handlers to the underlying `HttpClient` (logging, a proxy), call
`services.AddHttpClient(name)` with the same name before or after `AddClient`, and configure it there.

## Lifetimes

| Client | Lifetime | Why |
|---|---|---|
| `FluentBaseService` | `Transient` (default) or `Singleton` | No per-instance state besides the configuration |
| `FluentBaseSecureService` | `Scoped` in a web API; one instance per job in a worker | Holds the tokens. `Singleton` is refused (unsynchronised token state); `Transient` logs in for every resolved instance |

A secure client resolved once inside a long-lived singleton becomes a de facto singleton: concurrent
calls race on the token refresh. Resolve it from a scope per unit of work.

## Timeouts

`RequestTimeoutInSeconds` bounds each attempt (login and refresh included), body read included. A
server that sends its headers and then stalls fails the call with `ResponseBodyNotReceivedApiException`
(a `NoResponseApiException` with the received `StatusCode`), **without retry**: the server already
handled the request. The named `HttpClient` has an infinite timeout, so this setting and the caller's
token are the only bounds. A caller cancelling its token still gets `OperationCanceledException`.

With the default retry policy, one failing phase can take 4 × timeout + 2 + 4 + 8 seconds. A secure
client may chain several phases (login, request, refresh, re-login, resent request), each with that
budget. Keep the total inside the caller's own deadline (an HTTP request, a RabbitMQ consumer's
acknowledgment timeout).

## Retries

The default `FluentRetryPolicyBuilder` (Polly) retries on 408, 502, 503, 504, a timeout, or no
response, 3 times, waiting 2, 4 and 8 seconds. A failure after the status was received (body not
received) is never retried. It retries **every verb**. Before relying on it for a
POST, PUT or DELETE, make sure the server treats a repeat as harmless (idempotency key, upsert, natural
key), or remove the retries for that client.

Change it:
- override `protected virtual int MaxRetryAttemptOnTransientFailure` (read in the base constructor:
  return a constant, never a field of the derived class);
- pass an `IRetryPolicyBuilder` to `FluentBaseService(configuration, policyBuilder, clientFactory, apiPrefix)`;
  subclass `FluentRetryPolicyBuilder` and override `BuildRetryPolicy` or `CalculateNextAttemptDelay`;
- `FluentBaseSecureService` has no constructor taking both a factory and a policy builder; assign the
  protected `RetryPolicy` field in your constructor instead.

The policy writes each retry to `Console`, not to `ILogger`. Its waits do not observe the caller's
`CancellationToken`.
