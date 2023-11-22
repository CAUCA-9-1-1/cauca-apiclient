using Cauca.ApiClient.Configuration;

namespace Cauca.ApiClient.Tests.Mocks
{
    public class MockBaseApiClientConfiguration : BaseApiClientConfiguration
    {
        public override int RequestTimeoutInSeconds { get; set; } = 300;
    }
}
