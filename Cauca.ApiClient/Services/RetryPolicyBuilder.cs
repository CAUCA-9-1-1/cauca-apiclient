using System;
using System.Linq;
using System.Net;
using Cauca.ApiClient.Configuration;
using Cauca.ApiClient.Extensions;
using Flurl.Http;
using Polly;

namespace Cauca.ApiClient.Services
{
    public class RetryPolicyBuilder : IRetryPolicyBuilder
    {
        public virtual IAsyncPolicy BuildRetryPolicy(int maxRetryAttemptOnTransientFailure)
        {
            var retryPolicy = Policy
               .Handle<FlurlHttpException>(IsTransientOrTimeOut)
               .WaitAndRetryAsync(maxRetryAttemptOnTransientFailure, retryAttempt =>
               {
                   var nextAttemptIn = CalculateNextAttemptDelay(retryAttempt);
                   Console.WriteLine($"Retry attempt {retryAttempt} to make request. Next try on {nextAttemptIn.TotalSeconds} seconds.");
                   return nextAttemptIn;
               });

            return retryPolicy;
        }

        private bool IsTransientOrTimeOut(FlurlHttpException exception)
        {
            if (IsTransientError(exception) || exception.Call.NoResponse())
            {
                var statusCode = exception.StatusCode.HasValue ? exception.StatusCode.Value : 0;
                Console.WriteLine($"An exception append with status code: {statusCode} and it will be retry: {exception.Message}");
                return true;
            }

            return false;
        }

        private bool IsTransientError(FlurlHttpException exception)
        {
            int[] httpStatusCodesWorthRetrying =
            {
                (int)HttpStatusCode.RequestTimeout, // 408
                (int)HttpStatusCode.BadGateway, // 502
                (int)HttpStatusCode.ServiceUnavailable, // 503
                (int)HttpStatusCode.GatewayTimeout // 504
            };

            return exception.StatusCode.HasValue && httpStatusCodesWorthRetrying.Contains(exception.StatusCode.Value);
        }

        protected virtual TimeSpan CalculateNextAttemptDelay(int retryAttempt)
        {
            return TimeSpan.FromSeconds(Math.Pow(2, retryAttempt));
        }
    }
}