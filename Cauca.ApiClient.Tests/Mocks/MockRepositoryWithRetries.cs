using Cauca.ApiClient.Configuration;
using Cauca.ApiClient.Services;

namespace Cauca.ApiClient.Tests.Mocks
{
    public class MockRepositoryWithRetries : BaseService<MockConfiguration>
    {
        public MockRepositoryWithRetries(MockConfiguration configuration, IRetryPolicyBuilder policyBuilder = null) : base(configuration, policyBuilder)
        {
        }
    }
}