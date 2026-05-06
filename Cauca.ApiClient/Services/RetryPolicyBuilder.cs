using System;
using System.Net;
using Cauca.ApiClient.Configuration;
using Cauca.ApiClient.Extensions;
using Flurl.Http;
using Polly;

namespace Cauca.ApiClient.Services;

public class RetryPolicyBuilder : IRetryPolicyBuilder
{
    public virtual IAsyncPolicy BuildRetryPolicy(int maxRetryAttemptOnTransientFailure)
    {
        var retryPolicy = Policy
            .Handle<FlurlHttpException>(IsTransientOrTimeOut)
            .WaitAndRetryAsync(
                maxRetryAttemptOnTransientFailure,
                CalculateNextAttemptDelay,
                (exception, nextAttemptIn, retryAttempt, _) =>
                {
                    var flurlException = (FlurlHttpException)exception;
                    var url = flurlException.Call?.Request?.Url?.ToString() ?? "unknown url";
                    var statusCode = flurlException.StatusCode?.ToString() ?? "no response";
                    Console.WriteLine($"Retry attempt {retryAttempt} for {url} returned {statusCode}. Next try in {nextAttemptIn.TotalSeconds} seconds.");
                });

        return retryPolicy;
    }

    private static bool IsTransientOrTimeOut(FlurlHttpException exception)
    {
        return IsTransientError(exception) || exception.Call.NoResponse();
    }

    private static bool IsTransientError(FlurlHttpException exception)
    {
        int[] httpStatusCodesWorthRetrying =
        [
            (int)HttpStatusCode.RequestTimeout, // 408
            (int)HttpStatusCode.BadGateway, // 502
            (int)HttpStatusCode.ServiceUnavailable, // 503
            (int)HttpStatusCode.GatewayTimeout // 504
        ];

        return exception.StatusCode.HasValue && httpStatusCodesWorthRetrying.Contains(exception.StatusCode.Value);
    }

    protected virtual TimeSpan CalculateNextAttemptDelay(int retryAttempt)
    {
        return TimeSpan.FromSeconds(Math.Pow(2, retryAttempt));
    }
}