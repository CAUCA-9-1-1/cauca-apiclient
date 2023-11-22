using Cauca.ApiClient.Services;

namespace Cauca.ApiClient.Tests.Mocks
{
    public class MockRepository : BaseService<MockBaseApiClientConfiguration>
    {
        public MockRepository(MockBaseApiClientConfiguration baseApiClientConfiguration) : base(baseApiClientConfiguration)
        {
        }
    }
}