using System;
using System.Net.Http;
using System.Threading.Tasks;
using Cauca.ApiClient.Services;

namespace Cauca.ApiClient.Tests.Mocks
{
    public class MockSecureRepository : FluentBaseSecureService<MockConfiguration>
    {
        protected override int MaxRetryAttemptOnTransientFailure => 0;

        public MockSecureRepository(MockConfiguration configuration) : base(configuration)
        {
        }

        public MockSecureRepository(MockConfiguration configuration, Func<HttpClient> client, string apiPrefix = null)
            : base(configuration, client, apiPrefix)
        {
        }

        public Task<MockResponse> PostMockAsync(MockEntity entity)
        {
            return Request("mock")
                .WithBody(entity)
                .PostAsync<MockResponse>();
        }

        public Task<MockResponse> GetGeographyCitiesWithQueryAndHeadersAsync()
        {
            return Request("geography")
                .AppendSegments(10, "cities")
                .AddQueryParameter("Top", 20)
                .WithHeaders(new { Extra = "value" })
                .GetAsync<MockResponse>();
        }
    }
}