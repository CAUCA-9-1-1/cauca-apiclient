# Cauca.ApiClient

`Cauca.ApiClient` now supports two API client styles in the same package:

- Legacy Flurl-based base classes for backward compatibility.
- New `HttpClient`-based fluent base classes for new development.

## Package direction

Use the fluent base classes for all new clients:

- `FluentBaseService<TConfiguration>`
- `FluentBaseSecureService<TConfiguration>`

The legacy base classes are still available but marked obsolete:

- `BaseService<TConfiguration>`
- `BaseSecureService<TConfiguration>`

This keeps existing integrations working while allowing incremental migration, but it is a temporary compatibility bridge only.

## Legacy removal warning

The Flurl-based legacy base classes are scheduled for removal in a future major release.

If your client still inherits from:

- `BaseService<TConfiguration>`
- `BaseSecureService<TConfiguration>`

you should plan and start the migration now. Do not treat the obsolete legacy API as a long-term option. New clients should be built on the fluent base classes immediately, and existing clients should be moved as fast as your release cadence allows.

## Configuration

Implement `IConfiguration` with at least:

```csharp
public class MyConfiguration : IConfiguration
{
	public string ApiBaseUrl { get; set; }
	public string ApiBaseUrlForAuthentication { get; set; }
	public string UserId { get; set; }
	public string Password { get; set; }
}
```

## Fluent client example

```csharp
public class OrdersClient : FluentBaseSecureService<MyConfiguration>
{
	public OrdersClient(MyConfiguration configuration, Func<HttpClient> clientFactory)
		: base(configuration, clientFactory, "api")
	{
	}

	public Task<OrderDto> GetOrder(Guid orderId)
	{
		return Request("orders")
			.AppendSegment(orderId)
			.GetAsync<OrderDto>();
	}

	public Task<PagedResult<OrderDto>> SearchOrders(OrderQuery query)
	{
		return Request("orders")
			.WithQueryParameters(query)
			.GetAsync<PagedResult<OrderDto>>();
	}

	public Task<OrderDto> CreateOrder(CreateOrderCommand command)
	{
		return Request("orders")
			.WithBody(command)
			.PostAsync<OrderDto>();
	}
}
```

## Fluent request features

The fluent builder supports:

```csharp
var order = await Request("orders")
	.AppendSegments(customerId, "history")
	.AddQueryParameter("page", 2)
	.WithQueryParameters(new { pageSize = 20, includeInactive = false })
	.WithHeader("X-Correlation-Id", correlationId)
	.WithHeaders(new { Region = "ca" })
	.GetAsync<OrderDto>();
```

For commands with a request body:

```csharp
var created = await Request("orders")
	.WithBody(command)
	.PostAsync<OrderDto>();
```

## Dependency injection

Register regular clients:

```csharp
services.AddClient<OrdersClient>();
```

Register secure clients:

```csharp
services.AddSecureClient<OrdersClient>();
```

Named clients are supported internally through `IHttpClientFactory`. The registration helpers work with both fluent and legacy constructor shapes so migration can happen per-client instead of all at once, but that compatibility is intended to help you move off Flurl rather than remain on it.

## Legacy clients

If you still inherit from `BaseService<TConfiguration>` or `BaseSecureService<TConfiguration>`, your clients continue to work for now. Those APIs are deprecated, will be removed, and should be migrated to the fluent equivalents as soon as possible.

## Migration

See [docs/UPGRADING.md](docs/UPGRADING.md) for the detailed migration guide.

The repository also includes AI-oriented migration instructions for assisted refactors:

- [CLAUDE.md](CLAUDE.md)
- [.github/skills/cauca-apiclient-fluent-migration/SKILL.md](.github/skills/cauca-apiclient-fluent-migration/SKILL.md)
