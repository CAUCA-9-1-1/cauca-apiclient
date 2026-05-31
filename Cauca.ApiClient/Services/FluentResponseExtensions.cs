using System.Net;
using System.Net.Http;
using Cauca.ApiClient.Extensions;

namespace Cauca.ApiClient.Services;

internal static class FluentResponseExtensions
{
    public static bool IsUnauthorized(this ApiHttpException response)
    {
        return response.StatusCode == HttpStatusCode.Unauthorized;
    }

    public static bool RefreshTokenIsExpired(this ApiHttpException response)
    {
        return response.StatusCode == HttpStatusCode.Unauthorized
            && response.ResponseHeaders.Contains(RestResponseExtensions.RefreshTokenExpired);
    }

    public static bool RefreshTokenIsInvalid(this ApiHttpException response)
    {
        return response.StatusCode == HttpStatusCode.Unauthorized
            && response.ResponseHeaders.Contains(RestResponseExtensions.RefreshTokenInvalid);
    }

    public static bool AccessTokenIsExpired(this ApiHttpException response)
    {
        return response.StatusCode == HttpStatusCode.Unauthorized
            && response.ResponseHeaders.Contains(RestResponseExtensions.AccessTokenExpired);
    }

    public static bool NoResponse(this ApiHttpException response)
    {
        return !response.AnswerReceived;
    }

    public static bool IsUnauthorized(this HttpResponseMessage response)
    {
        return response?.StatusCode == HttpStatusCode.Unauthorized;
    }

    public static bool RefreshTokenIsExpired(this HttpResponseMessage response)
    {
        return response.IsUnauthorized() && response.Headers.Contains(RestResponseExtensions.RefreshTokenExpired);
    }

    public static bool RefreshTokenIsInvalid(this HttpResponseMessage response)
    {
        return response.IsUnauthorized() && response.Headers.Contains(RestResponseExtensions.RefreshTokenInvalid);
    }

    public static bool AccessTokenIsExpired(this HttpResponseMessage response)
    {
        return response.IsUnauthorized() && response.Headers.Contains(RestResponseExtensions.AccessTokenExpired);
    }
}
