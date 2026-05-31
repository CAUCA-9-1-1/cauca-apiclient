# Cauca.ApiClient Fluent Migration Skill

Use this skill when migrating code from the obsolete Flurl-based base services to the fluent `HttpClient`-based API.

## Goal

Convert clients that inherit from legacy base classes into clients that inherit from:

- `FluentBaseService<TConfiguration>`
- `FluentBaseSecureService<TConfiguration>`

## Required migration behavior

1. Preserve existing endpoint behavior.
2. Preserve request and response types.
3. Preserve authentication behavior for secure clients.
4. Replace direct verb helpers with `Request("...")` fluent chains.
5. Replace path concatenation with `AppendSegment(...)` or `AppendSegments(...)`.
6. Replace query object arguments with `AddQueryParameter(...)` or `WithQueryParameters(...)`.
7. Replace ad hoc header plumbing with `WithHeader(...)` or `WithHeaders(...)`.
8. Replace explicit payload arguments with `WithBody(...)`.
9. Keep changes focused; do not refactor unrelated code.

## Migration patterns

### GET

```csharp
await Request("orders")
    .AppendSegment(orderId)
    .GetAsync<OrderDto>();
```

### GET with query string

```csharp
await Request("orders")
    .WithQueryParameters(new { page = 1, pageSize = 25 })
    .GetAsync<PagedResult<OrderDto>>();
```

### POST

```csharp
await Request("orders")
    .WithBody(command)
    .PostAsync<OrderDto>();
```

### PUT

```csharp
await Request("orders")
    .AppendSegment(orderId)
    .WithBody(command)
    .PutAsync<OrderDto>();
```

### DELETE

```csharp
await Request("orders")
    .AppendSegment(orderId)
    .DeleteAsync();
```

## DI guidance

Use:

```csharp
services.AddClient<MyClient>();
services.AddSecureClient<MySecureClient>();
```

The registration helpers support both fluent and legacy constructor shapes to allow incremental migration.

## Verification checklist

After a migration, verify:

- URL path composition
- query string behavior
- headers
- serialized body content
- secure login and refresh behavior
- DI registration

## Source of truth

Consult `docs/UPGRADING.md` in this repository for the full migration guide.
