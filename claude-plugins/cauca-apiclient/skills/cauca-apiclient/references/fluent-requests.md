# Fluent requests

Inside a class deriving from `FluentBaseService<T>` or `FluentBaseSecureService<T>`, `Request(route)`
(protected) returns a `FluentRequestBuilder`. Each call builds one request; the builder is not meant
to be reused.

## URL

Final URL: `{ApiBaseUrl}/{apiPrefix}/{route}/{segment}/{segment}?{query}`. The trailing slash of the
base URL and the slashes at both ends of the prefix and the route are trimmed, then the parts are
rejoined.

```csharp
Request("customers").AppendSegments(customerId, "orders", orderId)
// → {ApiBaseUrl}/{apiPrefix}/customers/{customerId}/orders/{orderId}
```

- `AppendSegment(object)` / `AppendSegments(params object[])`: values pass through
  `Convert.ToString(value, InvariantCulture)`. `null` segments are skipped, which shortens the route
  without an error. Check required ids before the call.
- Each segment is trimmed of leading and trailing `/` (`"/details"` → `details`), then escaped with
  `Uri.EscapeDataString`: an inner `/`, `?`, `#`, spaces and `%` are encoded, so one value is exactly
  one segment (`AppendSegment("a/b?c")` → `a%2Fb%3Fc`). Do not escape values yourself (`%` would
  become `%25`). To add several path pieces, pass them separately: `AppendSegments("a", "b")`.
- A segment equal to `.` or `..` (after trimming) throws `ArgumentException`: `System.Uri` would
  otherwise normalise it and call another endpoint. `...` or `a..b` are accepted.
- The route itself is not escaped. A query string in the route is kept and merged with the builder's
  parameters: `Request("orders?status=open").AddQueryParameter("page", 2)` → `orders?status=open&page=2`.
  A builder parameter whose name matches a route parameter (case-insensitive) replaces it; a `null`
  builder value removes it. Segments still go into the path:
  `Request("orders?status=open").AppendSegment(5)` → `orders/5?status=open`. Prefer the builder
  methods for new code.

## Query parameters

```csharp
Request("orders")
    .AddQueryParameter("page", 2)
    .WithQueryParameters(new { pageSize = 20, includeInactive = false })
```

- `WithQueryParameters(object)` accepts an anonymous or typed object (public instance properties,
  names used as written: `PageSize` stays `PageSize`), an `IDictionary`, or a sequence of
  `KeyValuePair<string, string>` / `KeyValuePair<string, object>`.
- Keys are case-insensitive; a later value replaces an earlier one. One key holds one value:
  `ids=1&ids=2` cannot be produced.
- `null` values are omitted. Values are formatted with `Convert.ToString(value, InvariantCulture)` and
  escaped with `Uri.EscapeDataString`:
  - `bool` → `True` / `False`; enum → its name; `Guid` → `D` format;
  - `DateTime` → `MM/dd/yyyy HH:mm:ss` (pass `date.ToString("O")` or the format the server expects);
  - collections → their type name. Join them yourself (`string.Join(",", ids)`) if the server accepts
    that, or send them in a body.

## Headers

`WithHeader(name, value)` and `WithHeaders(object)` (same shapes as the query). Names are
case-insensitive, `null` values are skipped, values are added to the request headers with
`TryAddWithoutValidation`. Content headers (`Content-Type`, `Content-Language`…) are **silently
dropped** there. A secure client adds `Authorization` itself: do not set it.

## Body

`WithBody(object)` serializes with `System.Text.Json`, web defaults (camelCase names, case-insensitive
reads), always as `application/json; charset=utf-8`; another media type
(`application/json-patch+json`…) cannot be sent. A `GET` with a body throws `InvalidOperationException`.

Files: the protected helpers `PostFileAsync(url, fileName, stream, contentType)` and
`PostFileAsync(url, fileFullPath, fileName)`, with or without a `<T>` result, send
`multipart/form-data`. The stream is read from position 0 and buffered in memory. Multipart is not
otherwise exposed on the builder, and a request cannot have both a JSON body and multipart content.

## Sending

| Method | Verb | Result |
|---|---|---|
| `GetAsync()`, `GetAsync<T>()` | GET | none / `T` |
| `GetStringAsync()`, `GetBytesAsync()`, `GetStreamAsync()` | GET | raw content |
| `PostAsync()`, `PostAsync<T>()` | POST | none / `T` |
| `PostAndReceiveBytesAsync()`, `PostAndReceiveStreamAsync()` | POST | raw content |
| `PutAsync<T>()` | PUT | `T` (no non-generic overload) |
| `PatchAsync<T>()` | PATCH | `T` (no non-generic overload) |
| `DeleteAsync()`, `DeleteAsync<T>()` | DELETE | none / `T` |

Every method takes an optional `CancellationToken`. Pass the caller's token: it cancels a running
attempt and the reading of the body. The waits between retries (2, 4, 8 s) are not cancellable: a
cancellation during a wait takes effect when the next attempt starts.

How `T` is read:
- `string` → raw text; `byte[]` → bytes; `Stream` → a `MemoryStream` copy (the whole body in memory);
- `bool` → `true` only when the text is `true` (case-insensitive);
- `int` → `int.TryParse`, **`0` when the body is not an integer** (no error);
- anything else → JSON with web defaults. An empty body throws a `JsonException`, after the server has
  done the work and without retry. For an endpoint answering 204 No Content, use the non-generic
  method, or `PutAsync<string>()` / `PatchAsync<string>()` since those have no non-generic form.

## Errors

Status codes are mapped after retries (see [registration-and-resilience.md](registration-and-resilience.md)):

| Status | Exception (namespace `Cauca.ApiClient.Exceptions`) |
|---|---|
| 400 | `BadParameterApiException` |
| 401 | `UnauthorizedApiException` |
| 403 | `ForbiddenApiException` |
| 404 | `NotFoundApiException` |
| 500 | `InternalErrorApiException` |
| no response, timeout | `NoResponseApiException` |
| status received, body not received (timeout or cut while reading it) | `ResponseBodyNotReceivedApiException` (derives from `NoResponseApiException`, has `StatusCode`); never retried |
| any other non-2xx (409, 422, 429, 503 after retries…) | `UnexpectedStatusCodeApiException`, with `StatusCode` |

On a secure client, the request resent after a token refresh goes through the same mapping.

All mapped exceptions derive from `ApiClientException`. For 400, 403, 404, 500 and unexpected codes
the server's body is in the `Body` property (`ResponseBody` stays `null`); `UnauthorizedApiException`
and `NoResponseApiException` carry none. The helpers read it too:

```csharp
catch (BadParameterApiException exception)
{
    var problem = await exception.GetResponseAsync<ValidationProblem>();   // case-sensitive names
    var raw = await exception.GetResponseStringAsync();
}
```

`GetResponseAsync<T>()` uses `JsonSerializer.Deserialize<T>(body)` without options: a camelCase body
does not fill PascalCase properties. Read the string and deserialize with
`new JsonSerializerOptions(JsonSerializerDefaults.Web)` when the server sends camelCase.

A caller cancelling its token gets an `OperationCanceledException`, not `NoResponseApiException`.
