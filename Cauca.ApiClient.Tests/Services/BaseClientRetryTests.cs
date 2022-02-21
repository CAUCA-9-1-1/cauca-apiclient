using Cauca.ApiClient.Configuration;
using Cauca.ApiClient.Tests.Mocks;
using Flurl.Http.Testing;
using NUnit.Framework;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Cauca.ApiClient.Tests.Services
{
    [TestFixture]
    internal class BaseClientRetryTests
    {
        private IRetryPolicyBuilder builder;
        private MockRepositoryWithRetries service;

        [SetUp]
        public void SetUpTest()
        {
            builder = new InstantRetryBuilder();
            var configuration = new MockConfiguration
            {
                ApiBaseUrl = "http://test/",
                AccessToken = "Token",
                RefreshToken = "RefreshToken",
                AuthorizationType = "Mock"
            };
            service = new MockRepositoryWithRetries(configuration, builder);
        }

        [TestCase(HttpStatusCode.RequestTimeout)]
        [TestCase(HttpStatusCode.BadGateway)]
        [TestCase(HttpStatusCode.ServiceUnavailable)]
        [TestCase(HttpStatusCode.GatewayTimeout)]
        public async Task TransientFailure_WhenDoingHttpCalls_ShouldRetry(HttpStatusCode code)
        {
            using var httpTest = new HttpTest();
            httpTest
                .RespondWith(status: (int)code)
                .RespondWith(status: (int)code)
                .RespondWith(status: (int)code)
                .RespondWith("Allo");

            var response = await service.GetStringAsync("mock");

            httpTest.ShouldHaveCalled("http://test/mock")
                .WithVerb(HttpMethod.Get)
                .Times(4);

            Assert.AreEqual(response, "Allo");
        }

        [Test]
        public async Task TransientFailure_WhenDoingHttpCalls_ShouldRetry()
        {
            using var httpTest = new HttpTest();
            httpTest
                .SimulateTimeout()
                .SimulateTimeout()
                .SimulateTimeout()
                .RespondWith("Allo");

            var response = await service.GetStringAsync("mock");

            httpTest.ShouldHaveCalled("http://test/mock")
                .WithVerb(HttpMethod.Get)
                .Times(4);

            Assert.AreEqual(response, "Allo");
        }
    }
}
