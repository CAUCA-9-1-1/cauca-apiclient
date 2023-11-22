using Cauca.ApiClient.Services;

namespace Cauca.ApiClient.Tests.Mocks
{
    public class MockSecureRepository : BaseSecureService<MockBaseApiClientConfiguration>
    {
        public MockSecureRepository(MockBaseApiClientConfiguration baseApiClientConfiguration) : base(baseApiClientConfiguration)
        {
        }
    }
}