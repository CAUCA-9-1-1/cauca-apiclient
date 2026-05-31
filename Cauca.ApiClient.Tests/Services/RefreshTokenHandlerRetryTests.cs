using System.Threading.Tasks;
using Cauca.ApiClient.Configuration;
using Cauca.ApiClient.Services;
using Cauca.ApiClient.Tests.Helpers;
using Cauca.ApiClient.Tests.Mocks;
using NUnit.Framework;
using Polly;

namespace Cauca.ApiClient.Tests.Services;

[TestFixture]
internal class RefreshTokenHandlerRetryTests
{
    private IConfiguration configuration;
    private IAsyncPolicy twoRetryPolicy;
    private AccessInformation accessInformation;

    [SetUp]
    public void SetupTest()
    {
        twoRetryPolicy = new InstantRetryBuilder().BuildRetryPolicy(2);
        accessInformation = new AccessInformation
        {
            AccessToken = "accesstoken",
            RefreshToken = "refreshtoken",
            AuthorizationType = "bearer"
        };
        configuration = new MockConfiguration
        {
            ApiBaseUrl = "http://test",
            UseExternalSystemLogin = false,
            UserId = "user",
            Password = "password"
        };
    }

    [Test]
    public async Task OnTransientFailure_WhenLoggingIn_ShouldRetry()
    {
        var loginResult = new LoginResult { AuthorizationType = "Bearer", RefreshToken = "NewRefreshToken", AccessToken = "NewAccessToken" };
        var handler = new TestHttpMessageHandler();
        handler.EnqueueResponse(System.Net.HttpStatusCode.BadGateway);
        handler.EnqueueJsonResponse(loginResult);
        var tokenHandler = new RefreshTokenHandler(configuration, accessInformation, twoRetryPolicy, handler.CreateClientFactory());

        await tokenHandler.Login();

        Assert.That(handler.Requests.Count, Is.EqualTo(2));
        Assert.That(handler.Requests[0].RequestUri, Is.EqualTo($"{configuration.ApiBaseUrl}/Authentication/logon"));
        Assert.That(handler.Requests[1].RequestUri, Is.EqualTo($"{configuration.ApiBaseUrl}/Authentication/logon"));
    }

    [Test]
    public async Task OnConnectionTimeOut_WhenLoggingIn_ShouldRetry()
    {
        var loginResult = new LoginResult { AuthorizationType = "Bearer", RefreshToken = "NewRefreshToken", AccessToken = "NewAccessToken" };
        var handler = new TestHttpMessageHandler();
        handler.EnqueueTimeout();
        handler.EnqueueJsonResponse(loginResult);
        var tokenHandler = new RefreshTokenHandler(configuration, accessInformation, twoRetryPolicy, handler.CreateClientFactory());

        await tokenHandler.Login();

        Assert.That(handler.Requests.Count, Is.EqualTo(2));
        Assert.That(handler.Requests[0].RequestUri, Is.EqualTo($"{configuration.ApiBaseUrl}/Authentication/logon"));
        Assert.That(handler.Requests[1].RequestUri, Is.EqualTo($"{configuration.ApiBaseUrl}/Authentication/logon"));
    }

    [Test]
    public async Task OnTransientFailure_WhenRefreshingToken_ShouldRetry()
    {
        var refreshResult = new TokenRefreshResult { AccessToken = "NewAccessToken" };
        var handler = new TestHttpMessageHandler();
        handler.EnqueueResponse(System.Net.HttpStatusCode.BadGateway);
        handler.EnqueueJsonResponse(refreshResult);
        var tokenHandler = new RefreshTokenHandler(configuration, accessInformation, twoRetryPolicy, handler.CreateClientFactory());

        await tokenHandler.RefreshToken();

        Assert.That(handler.Requests.Count, Is.EqualTo(2));
        Assert.That(handler.Requests[0].RequestUri, Is.EqualTo($"{configuration.ApiBaseUrl}/Authentication/refresh"));
        Assert.That(handler.Requests[1].RequestUri, Is.EqualTo($"{configuration.ApiBaseUrl}/Authentication/refresh"));
    }
}
