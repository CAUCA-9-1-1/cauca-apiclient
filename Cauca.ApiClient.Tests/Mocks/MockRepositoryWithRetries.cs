using System;
using System.Net.Http;
using System.Threading.Tasks;
using Cauca.ApiClient.Configuration;
using Cauca.ApiClient.Services;

namespace Cauca.ApiClient.Tests.Mocks
{
    public class MockRepositoryWithRetries : FluentBaseService<MockConfiguration>
    {
        public MockRepositoryWithRetries(MockConfiguration configuration, IRetryPolicyBuilder policyBuilder = null) : base(configuration, policyBuilder)
        {
        }

        public MockRepositoryWithRetries(MockConfiguration configuration, IRetryPolicyBuilder policyBuilder, Func<HttpClient> client, string apiPrefix = null)
            : base(configuration, policyBuilder, client, apiPrefix)
        {
        }

        public Task<string> GetMockStringAsync()
        {
            return Request("mock").GetStringAsync();
        }
    }
}