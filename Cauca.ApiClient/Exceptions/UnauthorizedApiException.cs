using System;

namespace Cauca.ApiClient.Exceptions;

public class UnauthorizedApiException(string url, Exception innerException = null)
    : ApiClientException($"API returned a 401 (unauthorized) response for url '{url}'.", innerException);