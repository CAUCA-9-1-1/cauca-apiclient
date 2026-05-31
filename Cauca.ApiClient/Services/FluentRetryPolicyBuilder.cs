using System;
using System.Net;
using Cauca.ApiClient.Configuration;
using Polly;

namespace Cauca.ApiClient.Services;

public class FluentRetryPolicyBuilder : IRetryPolicyBuilder
{
    public virtual IAsyncPolicy BuildRetryPolicy(int maxRetryAttemptOnTransientFailure)
    {
        return Policy
            .Handle<ApiHttpException>(IsTransientOrTimeOut)
            .WaitAndRetryAsync(
                maxRetryAttemptOnTransientFailure,
                CalculateNextAttemptDelay,
                (exception, nextAttemptIn, retryAttempt, _) =>
                {
                    var apiException = (ApiHttpException)exception;
                    var url = apiException.RequestUri ?? "unknown url";
                    var statusCode = apiException.StatusCode?.ToString() ?? "no response";
                    Console.WriteLine($"Retry attempt {retryAttempt} for {url} returned {statusCode}. Next try in {nextAttemptIn.TotalSeconds} seconds.");
                });
    }

    private static bool IsTransientOrTimeOut(ApiHttpException exception)
    {
        return IsTransientError(exception) || exception.NoResponse();
    }

    private static bool IsTransientError(ApiHttpException exception)
    {
        int[] httpStatusCodesWorthRetrying =
        [
            (int)HttpStatusCode.RequestTimeout,
            (int)HttpStatusCode.BadGateway,
            (int)HttpStatusCode.ServiceUnavailable,
            (int)HttpStatusCode.GatewayTimeout
        ];

        return exception.StatusCode.HasValue && httpStatusCodesWorthRetrying.Contains((int)exception.StatusCode.Value);
    }

    protected virtual TimeSpan CalculateNextAttemptDelay(int retryAttempt)
    {
        return TimeSpan.FromSeconds(Math.Pow(2, retryAttempt));
    }
}
