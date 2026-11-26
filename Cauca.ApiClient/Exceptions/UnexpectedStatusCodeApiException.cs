using System;
using System.Net;

namespace Cauca.ApiClient.Exceptions;

public class UnexpectedStatusCodeApiException(string url, HttpStatusCode statusCode, Exception innerException = null, string body = null)
    : ApiClientException($"API returned a {(int)statusCode} ({statusCode}) response for url '{url}'.", innerException, body)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}
