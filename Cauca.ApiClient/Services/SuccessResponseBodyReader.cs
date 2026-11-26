using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Cauca.ApiClient.Services;

internal static class SuccessResponseBodyReader
{
    public static async Task<TResult> ReadAsync<TResult>(
        HttpRequestMessage request,
        HttpResponseMessage response,
        Func<HttpResponseMessage, CancellationToken, Task<TResult>> responseReader,
        CancellationToken timeoutToken,
        CancellationToken callerToken)
    {
        try
        {
            return await responseReader(response, timeoutToken);
        }
        catch (Exception exception) when (IsBodyReadFailure(exception, callerToken))
        {
            throw ApiHttpException.ResponseBodyNotReceived(request, response.StatusCode, exception);
        }
    }

    private static bool IsBodyReadFailure(Exception exception, CancellationToken callerToken)
    {
        return exception switch
        {
            OperationCanceledException => !callerToken.IsCancellationRequested,
            HttpRequestException or IOException => true,
            _ => false
        };
    }
}
