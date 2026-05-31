using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Cauca.ApiClient.Services;
using Cauca.ApiClient.Tests.Helpers;
using Cauca.ApiClient.Tests.Mocks;
using FluentAssertions;
using NUnit.Framework;

namespace Cauca.ApiClient.Tests.Services;

[TestFixture]
internal class BaseClientRetryTests
{
    private IRetryPolicyBuilder builder;
    private MockConfiguration configuration;

    [SetUp]
    public void SetUpTest()
    {
        builder = new InstantRetryBuilder();
        configuration = new MockConfiguration
        {
            ApiBaseUrl = "http://test/"
        };
    }

    [TestCase(HttpStatusCode.RequestTimeout)]
    [TestCase(HttpStatusCode.BadGateway)]
    [TestCase(HttpStatusCode.ServiceUnavailable)]
    [TestCase(HttpStatusCode.GatewayTimeout)]
    public async Task TransientStatusCode_WhenDoingHttpCalls_ShouldRetry(HttpStatusCode code)
    {
        var handler = new TestHttpMessageHandler();
        handler.EnqueueResponse(code);
        handler.EnqueueResponse(code);
        handler.EnqueueResponse(code);
        handler.EnqueueResponse(body: "Allo");
        var service = new MockRepositoryWithRetries(configuration, builder, handler.CreateClientFactory());

        var response = await service.GetMockStringAsync();

        handler.Requests.Should().HaveCount(4);
        handler.Requests.Should().OnlyContain(request => request.Method == HttpMethod.Get && request.RequestUri == "http://test/mock");
        response.Should().Be("Allo");
    }

    [Test]
    public async Task Timeout_WhenDoingHttpCalls_ShouldRetry()
    {
        var handler = new TestHttpMessageHandler();
        handler.EnqueueTimeout();
        handler.EnqueueTimeout();
        handler.EnqueueTimeout();
        handler.EnqueueResponse(body: "Allo");
        var service = new MockRepositoryWithRetries(configuration, builder, handler.CreateClientFactory());

        var response = await service.GetMockStringAsync();

        handler.Requests.Should().HaveCount(4);
        handler.Requests.Should().OnlyContain(request => request.Method == HttpMethod.Get && request.RequestUri == "http://test/mock");
        response.Should().Be("Allo");
    }
}
