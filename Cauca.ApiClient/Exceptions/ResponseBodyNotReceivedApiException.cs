using System;
using System.Net;

namespace Cauca.ApiClient.Exceptions;

public class ResponseBodyNotReceivedApiException(string url, HttpStatusCode statusCode, Exception innerException = null)
    : NoResponseApiException($"API returned a {(int)statusCode} response for url '{url}' but its body was not received in a timely manner.", innerException)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}
