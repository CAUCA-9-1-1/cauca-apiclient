using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Cauca.ApiClient.Extensions;
using Cauca.ApiClient.Services;
using Cauca.ApiClient.Tests.Helpers;
using Cauca.ApiClient.Tests.Mocks;
using FluentAssertions;
using NUnit.Framework;
using Polly;

namespace Cauca.ApiClient.Tests.Services;

[TestFixture]
public class CaucaExternalSystemAuthHandlerTests
{
    private MockConfiguration configuration;

    [SetUp]
    public void SetupTest()
    {
        configuration = new MockConfiguration
        {
            ApiBaseUrl = "http://test/",
            UserId = "the-api-key",
            UseExternalSystemLogin = true
        };
    }

    private HttpClient CreateClient(TestHttpMessageHandler transport)
    {
        var handler = new CaucaExternalSystemAuthHandler(
            configuration,
            apiPrefix: null,
            authClientFactory: transport.CreateClientFactory(),
            policy: Policy.NoOpAsync())
        {
            InnerHandler = transport
        };
        return new HttpClient(handler) { BaseAddress = new System.Uri("http://test/") };
    }

    [Test]
    public async Task WhenNoToken_LogsInWithApiKeyThenAttachesBearerToken()
    {
        var transport = new TestHttpMessageHandler();
        transport.EnqueueJsonResponse(new LoginResult { AuthorizationType = "Bearer", AccessToken = "AccessToken", RefreshToken = "RefreshToken" });
        transport.EnqueueJsonResponse(new MockResponse());
        var client = CreateClient(transport);

        await client.GetAsync("mock");

        transport.Requests.Should().HaveCount(2);
        transport.Requests[0].RequestUri.Should().Be("http://test/Authentication/logonforexternalsystem");
        transport.Requests[0].Body.Should().Contain("the-api-key");
        transport.Requests[1].RequestUri.Should().Be("http://test/mock");
        transport.Requests[1].HasHeader("Authorization", "Bearer AccessToken").Should().BeTrue();
    }

    [Test]
    public async Task WhenAccessTokenExpired_RefreshesThenRetriesWithNewToken()
    {
        var transport = new TestHttpMessageHandler();
        transport.EnqueueJsonResponse(new LoginResult { AuthorizationType = "Bearer", AccessToken = "AccessToken", RefreshToken = "RefreshToken" });
        transport.EnqueueJsonResponse(new MockResponse(), HttpStatusCode.Unauthorized, (RestResponseExtensions.AccessTokenExpired, "True"));
        transport.EnqueueJsonResponse(new TokenRefreshResult { AccessToken = "RefreshedToken" });
        transport.EnqueueJsonResponse(new MockResponse());
        var client = CreateClient(transport);

        await client.GetAsync("mock");

        transport.Requests.Should().HaveCount(4);
        transport.Requests[0].RequestUri.Should().Be("http://test/Authentication/logonforexternalsystem");
        transport.Requests[1].RequestUri.Should().Be("http://test/mock");
        transport.Requests[2].RequestUri.Should().Be("http://test/Authentication/refreshforexternalsystem");
        transport.Requests[3].RequestUri.Should().Be("http://test/mock");
        transport.Requests[3].HasHeader("Authorization", "Bearer RefreshedToken").Should().BeTrue();
    }

    [Test]
    public async Task WhenRefreshTokenExpired_LogsBackInThenRetries()
    {
        var transport = new TestHttpMessageHandler();
        transport.EnqueueJsonResponse(new LoginResult { AuthorizationType = "Bearer", AccessToken = "AccessToken", RefreshToken = "RefreshToken" });
        transport.EnqueueJsonResponse(new MockResponse(), HttpStatusCode.Unauthorized, (RestResponseExtensions.RefreshTokenExpired, "True"));
        transport.EnqueueJsonResponse(new LoginResult { AuthorizationType = "Bearer", AccessToken = "SecondToken", RefreshToken = "SecondRefresh" });
        transport.EnqueueJsonResponse(new MockResponse());
        var client = CreateClient(transport);

        await client.GetAsync("mock");

        transport.Requests.Should().HaveCount(4);
        transport.Requests[1].RequestUri.Should().Be("http://test/mock");
        transport.Requests[2].RequestUri.Should().Be("http://test/Authentication/logonforexternalsystem");
        transport.Requests[3].HasHeader("Authorization", "Bearer SecondToken").Should().BeTrue();
    }
}
