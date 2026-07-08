# Cauca.ApiClient Migration Instructions for Claude

When updating code that uses this library, prefer the fluent API over the obsolete legacy base classes.

## Target API

Prefer:

- `FluentBaseService<TConfiguration>`
- `FluentBaseSecureService<TConfiguration>`

Avoid new code based on:

- `BaseService<TConfiguration>`
- `BaseSecureService<TConfiguration>`

## Migration rules

1. Replace direct HTTP verb helper calls with `Request("...")` builder chains.
2. Use `AppendSegment(...)` or `AppendSegments(...)` for path composition.
3. Use `AddQueryParameter(...)` for one-off query values.
4. Use `WithQueryParameters(...)` for object-based query groups.
5. Use `WithHeader(...)` and `WithHeaders(...)` for headers.
6. Use `WithBody(...)` for request bodies.
7. Preserve route semantics exactly during migration.
8. Preserve secure-client authentication behavior.
9. Keep migrations incremental; do not rename unrelated public types.

## Examples

```csharp
await Request("orders")
    .AppendSegment(orderId)
    .GetAsync<OrderDto>();
```

```csharp
await Request("orders")
    .WithQueryParameters(new { page = 1, pageSize = 25 })
    .GetAsync<PagedResult<OrderDto>>();
```

```csharp
await Request("orders")
    .WithBody(command)
    .PostAsync<OrderDto>();
```

## DI

Prefer:

```csharp
services.AddClient<MyClient>();
services.AddSecureClient<MySecureClient>();
```

## Reference

Use `docs/UPGRADING.md` in this repository as the source of truth for migration details.
