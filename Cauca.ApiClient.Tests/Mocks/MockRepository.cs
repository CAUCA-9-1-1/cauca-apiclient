using System;
using System.Net.Http;
using System.Threading.Tasks;
using Cauca.ApiClient.Services;

namespace Cauca.ApiClient.Tests.Mocks
{
    public class MockRepository : FluentBaseService<MockConfiguration>
    {
        protected override int MaxRetryAttemptOnTransientFailure => 0;

        public MockRepository(MockConfiguration configuration) : base(configuration)
        {
        }

        public MockRepository(MockConfiguration configuration, Func<HttpClient> client, string apiPrefix = null)
            : base(configuration, null, client, apiPrefix)
        {
        }

        public Task<MockResponse> PostMockAsync(MockEntity entity)
        {
            return Request("mock")
                .WithBody(entity)
                .PostAsync<MockResponse>();
        }

        public Task<MockResponse> PostMockWithHeadersAsync(MockEntity entity)
        {
            return Request("mock")
                .WithBody(entity)
                .WithHeader("X-Test", "one")
                .WithHeaders(new { Another = "two" })
                .PostAsync<MockResponse>();
        }

        public Task<string> GetMockStringWithPageAndFilterAsync()
        {
            return Request("mock")
                .AddQueryParameter("page", 3)
                .WithQueryParameters(new { filter = "open" })
                .GetStringAsync();
        }

        public Task<string> GetGeographyCitiesAsync()
        {
            return Request("geography")
                .AppendSegments(10, "cities")
                .GetStringAsync();
        }

        public Task<bool> GetMockBooleanAsync()
        {
            return Request("mock").GetAsync<bool>();
        }

        public Task<string> GetMockStringAsync()
        {
            return Request("mock").GetAsync<string>();
        }

        public Task<string> GetMockStringUsingShortcutAsync()
        {
            return Request("mock").GetStringAsync();
        }

        public Task<int> GetMockIntAsync()
        {
            return Request("mock").GetAsync<int>();
        }

        public Task<byte[]> GetMockBytesAsync()
        {
            return Request("mock").GetBytesAsync();
        }

        public Task DeleteMockAsync(MockEntity entity)
        {
            return Request("mock")
                .WithBody(entity)
                .DeleteAsync();
        }

        public Task<string> GetStringAsync(string url)
        {
            return Request(url).GetStringAsync();
        }
    }
}