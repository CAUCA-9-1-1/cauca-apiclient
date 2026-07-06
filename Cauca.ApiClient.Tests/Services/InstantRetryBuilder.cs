using Cauca.ApiClient.Services;
using System;

namespace Cauca.ApiClient.Tests.Services
{
    public class InstantRetryBuilder : FluentRetryPolicyBuilder
    {
        protected override TimeSpan CalculateNextAttemptDelay(int retryAttempt)
        {
            return TimeSpan.FromSeconds(0);
        }
    }
}
