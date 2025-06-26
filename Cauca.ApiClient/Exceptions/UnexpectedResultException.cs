using System;

namespace Cauca.ApiClient.Exceptions;

public class UnexpectedResultException(string url, string content, Exception innerException = null, string body = null)
    : ApiClientException($"API didn't return the expected result for '{url}'. Body content was: {content}.", innerException, body);