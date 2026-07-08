using System.Threading.Tasks;
using Cauca.ApiClient.Configuration;
using Cauca.ApiClient.Services;
using Cauca.ApiClient.Tests.Helpers;
using Cauca.ApiClient.Tests.Mocks;
using FluentAssertions;
using NUnit.Framework;
using Polly;

namespace Cauca.ApiClient.Tests.Services;

[TestFixture]
public class RefreshTokenHandlerTests
{
    private IConfiguration configuration;
    private IAsyncPolicy noRetryPolicy;
    private AccessInformation accessInformation;

    [SetUp]
    public void SetupTest()
    {
        noRetryPolicy = new LegacyInstantRetryBuilder().BuildRetryPolicy(0);
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

    [TestCase(true, "http://test/Authentication/refreshforexternalsystem")]
    [TestCase(false, "http://test/Authentication/refresh")]
    public async Task UrlIsCorrectlyGeneratedForExternalSystemAndNormalUserRefresh(bool useExternalSystem, string expectedUrl)
    {
        var handler = new TestHttpMessageHandler();
        handler.EnqueueJsonResponse(new TokenRefreshResult());
        configuration.UseExternalSystemLogin = useExternalSystem;
        var tokenHandler = new RefreshTokenHandler(configuration, accessInformation, noRetryPolicy, handler.CreateClientFactory());

        await tokenHandler.RefreshToken();

        handler.Requests.Should().ContainSingle().Which.RequestUri.Should().Be(expectedUrl);
    }

    [Test]
    public async Task AuthenticationUrlIsSet_WhenLoggingIn_ShouldUseBaseUrl()
    {
        var handler = new TestHttpMessageHandler();
        var loginResult = new LoginResult { AuthorizationType = "Bearer", RefreshToken = "NewRefreshToken", AccessToken = "NewAccessToken" };
        configuration.ApiBaseUrlForAuthentication = null;
        handler.EnqueueJsonResponse(loginResult);
        var tokenHandler = new RefreshTokenHandler(configuration, accessInformation, noRetryPolicy, handler.CreateClientFactory());

        await tokenHandler.Login();

        handler.Requests.Should().ContainSingle().Which.RequestUri.Should().Be($"{configuration.ApiBaseUrl}/Authentication/logon");
    }

    [Test]
    public async Task AuthenticationUrlIsSet_WhenLoggingIn_ShouldUseBaseAuthenticationUrl()
    {
        var handler = new TestHttpMessageHandler();
        var loginResult = new LoginResult { AuthorizationType = "Bearer", RefreshToken = "NewRefreshToken", AccessToken = "NewAccessToken" };
        configuration.ApiBaseUrlForAuthentication = "http://test/secureApi";
        handler.EnqueueJsonResponse(loginResult);
        var tokenHandler = new RefreshTokenHandler(configuration, accessInformation, noRetryPolicy, handler.CreateClientFactory());

        await tokenHandler.Login();

        handler.Requests.Should().ContainSingle().Which.RequestUri.Should().Be($"{configuration.ApiBaseUrlForAuthentication}/Authentication/logon");
    }

    [Test]
    public async Task NewAccessTokenIsCorrectlyCopiedInTheCurrentConfiguration()
    {
        var handler = new TestHttpMessageHandler();
        const string newToken = "newtoken";
        handler.EnqueueJsonResponse(new TokenRefreshResult { AccessToken = newToken });
        var tokenHandler = new RefreshTokenHandler(configuration, accessInformation, noRetryPolicy, handler.CreateClientFactory());

        await tokenHandler.RefreshToken();

        accessInformation.AccessToken.Should().Be(newToken);
    }

    [Test]
    public async Task NullIsCorrectlyReturnedForAnyOtherReason()
    {
        var handler = new TestHttpMessageHandler();
        handler.EnqueueJsonResponse(new TokenRefreshResult(), System.Net.HttpStatusCode.NotFound);
        var tokenHandler = new RefreshTokenHandler(configuration, accessInformation, noRetryPolicy, handler.CreateClientFactory());

        await tokenHandler.RefreshToken();

        accessInformation.AccessToken.Should().BeNull();
    }
}
