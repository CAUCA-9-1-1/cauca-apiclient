# Fluent requests

Inside a class deriving from `FluentBaseService<T>` or `FluentBaseSecureService<T>`, `Request(route)`
(protected) returns a `FluentRequestBuilder`. Each call builds one request; the builder is not meant
to be reused.

## URL

Final URL: `{ApiBaseUrl}/{apiPrefix}/{route}/{segment}/{segment}?{query}`. The trailing slash of the
base URL and the slashes at both ends of the prefix, the route and each segment are trimmed, then the
parts are rejoined.

```csharp
Request("customers").AppendSegments(customerId, "orders", orderId)
// → {ApiBaseUrl}/{apiPrefix}/customers/{customerId}/orders/{orderId}
```

- `AppendSegment(object)` / `AppendSegments(params object[])`: values pass through
  `Convert.ToString(value, InvariantCulture)`. `null` segments are skipped, which shortens the route
  without an error. Check required ids before the call.
- Segments are **not escaped**. `/`, `?` and `#` keep their URL meaning; `Uri` escapes only illegal
  characters such as spaces. Escape free-text values: `AppendSegment(Uri.EscapeDataString(name))`.
- Never put a query string in the route. The builder sets the URI query from its parameters and
  replaces whatever the route contained, even when there are no parameters.

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
| anything else | internal `ApiHttpException` — catch it as `Exception`; its message holds the URL and the status |

This mapping covers the first attempt of a secure client only. When the access token was expired, the
client refreshes and resends the request outside the mapping: any failure of the resent request
(401, 404, 500…) escapes as the internal `ApiHttpException`. Catch `Exception` as well around secure
calls.

All mapped exceptions derive from `ApiClientException`. For 400, 403, 404 and 500 the server's body is
in the `Body` property (`ResponseBody` stays `null`); `UnauthorizedApiException` and
`NoResponseApiException` carry none. The helpers read it too:

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
