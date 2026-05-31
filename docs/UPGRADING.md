# Upgrading to the Fluent API

## Summary

`Cauca.ApiClient` now exposes a new fluent API built on `HttpClient` while keeping the original Flurl-based base classes available for compatibility.

Use this migration path:

1. Keep existing legacy clients working as-is.
2. Move one client at a time to `FluentBaseService<TConfiguration>` or `FluentBaseSecureService<TConfiguration>`.
3. Replace direct verb calls with `Request("...")` builder chains.
4. Register migrated clients with `services.AddClient<TClient>()` or `services.AddSecureClient<TClient>()`.

## Base class changes

Old:

```csharp
public class OrdersClient : BaseSecureService<MyConfiguration>
```

New:

```csharp
public class OrdersClient : FluentBaseSecureService<MyConfiguration>
```

Constructor shape for fluent clients:

```csharp
public OrdersClient(MyConfiguration configuration, Func<HttpClient> clientFactory)
    : base(configuration, clientFactory, "api")
{
}
```

## Request migration patterns

### GET

Old:

```csharp
await GetAsync<OrderDto>("orders/123");
```

New:

```csharp
await Request("orders")
    .AppendSegment(123)
    .GetAsync<OrderDto>();
```

### GET with query parameters

Old:

```csharp
await GetAsync<PagedResult<OrderDto>>("orders", new { page = 2, pageSize = 20 });
```

New:

```csharp
await Request("orders")
    .AddQueryParameter("page", 2)
    .AddQueryParameter("pageSize", 20)
    .GetAsync<PagedResult<OrderDto>>();
```

Or:

```csharp
await Request("orders")
    .WithQueryParameters(new { page = 2, pageSize = 20 })
    .GetAsync<PagedResult<OrderDto>>();
```

### POST / PUT / DELETE with body

Old:

```csharp
await PostAsync<OrderDto>("orders", command);
await PutAsync<OrderDto>("orders/123", command);
await DeleteAsync("orders/123", command);
```

New:

```csharp
await Request("orders")
    .WithBody(command)
    .PostAsync<OrderDto>();

await Request("orders")
    .AppendSegment(123)
    .WithBody(command)
    .PutAsync<OrderDto>();

await Request("orders")
    .AppendSegment(123)
    .WithBody(command)
    .DeleteAsync();
```

### Headers

Old:

Headers usually required overriding request generation or using Flurl-specific APIs.

New:

```csharp
await Request("orders")
    .WithHeader("X-Correlation-Id", correlationId)
    .WithHeaders(new { Region = "ca" })
    .GetAsync<OrderDto>();
```

### Path segments

New path composition is explicit and safe:

```csharp
await Request("customers")
    .AppendSegments(customerId, "orders", orderId)
    .GetAsync<OrderDto>();
```

## Secure clients

`FluentBaseSecureService<TConfiguration>` keeps the same high-level behavior as the legacy secure base:

- automatic login when no token is present
- automatic refresh when the access token is expired
- automatic re-login when the refresh token is expired or invalid

Existing authentication configuration still applies.

## DI migration

For migrated clients:

```csharp
services.AddClient<MyClient>();
services.AddSecureClient<MySecureClient>();
```

These helpers support both fluent and legacy constructor shapes, so you can migrate incrementally.

## Recommended migration order

1. Migrate regular clients first.
2. Migrate secure clients after verifying authentication flows.
3. Replace route strings that manually concatenate path pieces with `AppendSegment` or `AppendSegments`.
4. Replace optional query-object arguments with `AddQueryParameter` or `WithQueryParameters`.
5. Replace manual request payload plumbing with `WithBody`.

## Validation checklist

After migrating a client, verify:

- request URLs still match the previous route structure
- query strings are still emitted correctly
- required headers are present
- body payloads serialize as expected
- secure clients still log in and refresh tokens correctly
- DI registration still resolves the client successfully
