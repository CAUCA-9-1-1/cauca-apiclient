using System;

namespace Cauca.ApiClient.Exceptions;

public class ForbiddenApiException(string url, Exception innerException = null, string body = null)
    : ApiClientException($"API returned a 403 (forbidden) response for url '{url}'.", innerException, body);