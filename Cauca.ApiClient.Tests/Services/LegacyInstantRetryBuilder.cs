using System;
using Cauca.ApiClient.Services;

namespace Cauca.ApiClient.Tests.Services
{
    public class LegacyInstantRetryBuilder : RetryPolicyBuilder
    {
        protected override TimeSpan CalculateNextAttemptDelay(int retryAttempt)
        {
            return TimeSpan.FromSeconds(0);
        }
    }
}
