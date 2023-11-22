using Cauca.ApiClient.Configuration;
using Cauca.ApiClient.Services;

namespace Cauca.ApiClient.Tests.Mocks
{
    public class MockRepositoryWithRetries : BaseService<MockBaseApiClientConfiguration>
    {
        public MockRepositoryWithRetries(MockBaseApiClientConfiguration baseApiClientConfiguration, IRetryPolicyBuilder policyBuilder = null) : base(baseApiClientConfiguration, policyBuilder)
        {
        }
    }
}