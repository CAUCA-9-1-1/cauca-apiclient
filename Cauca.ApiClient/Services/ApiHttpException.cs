using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Cauca.ApiClient.Services;

internal sealed class ApiHttpException : Exception
{
    public ApiHttpException(string requestUri, HttpStatusCode? statusCode, bool answerReceived, string responseBody = null, Exception innerException = null)
        : base(CreateMessage(requestUri, statusCode, answerReceived), innerException)
    {
        RequestUri = requestUri;
        StatusCode = statusCode;
        AnswerReceived = answerReceived;
        ResponseBody = responseBody;
    }

    public string RequestUri { get; }

    public HttpStatusCode? StatusCode { get; }

    public bool AnswerReceived { get; }

    public string ResponseBody { get; }

    public IReadOnlyCollection<string> ResponseHeaders { get; init; } = Array.Empty<string>();

    public static async Task<ApiHttpException> CreateAsync(HttpRequestMessage request, HttpResponseMessage response, CancellationToken cancellationToken, Exception innerException = null)
    {
        return new ApiHttpException(
            request.RequestUri?.ToString(),
            response?.StatusCode,
            response is not null,
            await ReadResponseBodyAsync(response, cancellationToken),
            innerException)
        {
            ResponseHeaders = GetHeaders(response)
        };
    }

    public static ApiHttpException NoResponse(HttpRequestMessage request, Exception innerException)
    {
        return new ApiHttpException(request.RequestUri?.ToString(), null, false, null, innerException);
    }

    private static async Task<string> ReadResponseBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response?.Content is null)
            return null;

        try
        {
            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    private static string CreateMessage(string requestUri, HttpStatusCode? statusCode, bool answerReceived)
    {
        if (!answerReceived)
            return $"Request to '{requestUri}' did not receive a response.";

        return $"Request to '{requestUri}' failed with status code {(int?)statusCode ?? 0}.";
    }

    private static IReadOnlyCollection<string> GetHeaders(HttpResponseMessage response)
    {
        if (response is null)
            return Array.Empty<string>();

        return response.Headers.Select(header => header.Key)
            .Concat(response.Content?.Headers.Select(header => header.Key) ?? [])
            .ToArray();
    }
}