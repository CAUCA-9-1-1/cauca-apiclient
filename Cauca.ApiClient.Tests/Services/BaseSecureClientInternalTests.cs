using Cauca.ApiClient.Tests.Mocks;
using FluentAssertions;
using NUnit.Framework;

namespace Cauca.ApiClient.Tests.Services
{
    [TestFixture]
    public class BaseSecureClientInternalTests : MockSecureRepository
    {
        public BaseSecureClientInternalTests() : base(new MockConfiguration
        {
            ApiBaseUrl = "http://test",
        })
        {
            AccessInformation.AccessToken = "Token";
            AccessInformation.AuthorizationType = "Mock";
        }

        [TestCase]
        public void AuthorizationHeaderIsCorrectlyGenerated()
        {
            var result = GetAuthorizationHeaderValue();

            result.Should().Be("Mock Token");
        }
    }
}