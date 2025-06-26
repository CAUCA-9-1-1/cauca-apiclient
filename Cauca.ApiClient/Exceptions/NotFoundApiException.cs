using System;

namespace Cauca.ApiClient.Exceptions;

public class NotFoundApiException(string url, Exception innerException = null, string body = null)
    : ApiClientException($"API returned a 404 (not found) response for url '{url}'.", innerException, body);