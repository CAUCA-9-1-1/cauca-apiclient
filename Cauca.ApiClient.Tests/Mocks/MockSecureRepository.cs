using Cauca.ApiClient.Services;

namespace Cauca.ApiClient.Tests.Mocks
{
    public class MockSecureRepository : BaseSecureService<MockConfiguration>
    {
        protected override int MaxRetryAttemptOnTransientFailure => 0;

        public MockSecureRepository(MockConfiguration configuration) : base(configuration)
        {
        }
    }
}