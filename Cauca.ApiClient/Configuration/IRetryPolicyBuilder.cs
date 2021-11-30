using Polly;

namespace Cauca.ApiClient.Configuration
{
    public interface IRetryPolicyBuilder
    {
        IAsyncPolicy BuildRetryPolicy(int maxRetryAttemptOnTransientFailure);
    }
}
