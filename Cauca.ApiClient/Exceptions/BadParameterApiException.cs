using System;

namespace Cauca.ApiClient.Exceptions;

public class BadParameterApiException(string url, Exception innerException = null, string body = null)
    : ApiClientException($"API returned a 400 (bad request) response for url '{url}'.", innerException, body);