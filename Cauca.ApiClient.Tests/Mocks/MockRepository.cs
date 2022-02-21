using Cauca.ApiClient.Services;

namespace Cauca.ApiClient.Tests.Mocks
{
    public class MockRepository : BaseService<MockConfiguration>
    {
        protected override int MaxRetryAttemptOnTransientFailure => 0;

        public MockRepository(MockConfiguration configuration) : base(configuration)
        {
        }
    }
}