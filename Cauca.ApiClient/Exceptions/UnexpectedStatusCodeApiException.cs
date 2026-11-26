using System;
using System.Net;

namespace Cauca.ApiClient.Exceptions;

public class UnexpectedStatusCodeApiException(string url, HttpStatusCode statusCode, Exception innerException = null, string body = null)
    : ApiClientException($"API returned a {DescribeStatusCode(statusCode)} response for url '{url}'.", innerException, body)
{
    public HttpStatusCode StatusCode { get; } = statusCode;

    private static string DescribeStatusCode(HttpStatusCode statusCode)
    {
        return Enum.IsDefined(statusCode) ? $"{(int)statusCode} ({statusCode})" : ((int)statusCode).ToString();
    }
}
