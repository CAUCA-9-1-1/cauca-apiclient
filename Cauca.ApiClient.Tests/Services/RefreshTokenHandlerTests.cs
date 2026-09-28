using System;
using System.Net;
using System.Threading.Tasks;
using Cauca.ApiClient.Configuration;
using Cauca.ApiClient.Exceptions;
using Cauca.ApiClient.Services;
using Cauca.ApiClient.Tests.Helpers;
using Cauca.ApiClient.Tests.Mocks;
using FluentAssertions;
using Flurl.Http;
using NUnit.Framework;
using Polly;

namespace Cauca.ApiClient.Tests.Services;

[TestFixture]
public class RefreshTokenHandlerTests
{
    private const string ApiKey = "secret-api-key-123";
    private const string Password = "secret-password-123";
    private const string LoginProcessErrorMessage = "API returned a 500 (internal error) response for url 'An error occured in the login process'.";

    private MockConfiguration configuration;
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
        handler.EnqueueJsonResponse(new TokenRefreshResult(), HttpStatusCode.NotFound);
        var tokenHandler = new RefreshTokenHandler(configuration, accessInformation, noRetryPolicy, handler.CreateClientFactory());

        await tokenHandler.RefreshToken();

        accessInformation.AccessToken.Should().BeNull();
    }

    [Test]
    public async Task LoginWithExternalSystemAndUnauthorizedResponseThrowsInvalidCredentialExceptionWithoutApiKey()
    {
        configuration.UseExternalSystemLogin = true;
        configuration.UserId = ApiKey;
        var handler = new TestHttpMessageHandler();
        handler.EnqueueResponse(HttpStatusCode.Unauthorized);
        var tokenHandler = new RefreshTokenHandler(configuration, accessInformation, noRetryPolicy, handler.CreateClientFactory());

        var action = () => tokenHandler.Login();

        var thrown = (await action.Should().ThrowAsync<InvalidCredentialException>()).Which;
        thrown.Message.Should().Be("The configured external system credentials were rejected.");
        AssertDoesNotLeak(thrown, ApiKey);
    }

    [Test]
    public async Task LoginWithUserPasswordAndUnauthorizedResponseStillContainsUsername()
    {
        configuration.UseExternalSystemLogin = false;
        configuration.UserId = "user";
        configuration.Password = Password;
        var handler = new TestHttpMessageHandler();
        handler.EnqueueResponse(HttpStatusCode.Unauthorized);
        var tokenHandler = new RefreshTokenHandler(configuration, accessInformation, noRetryPolicy, handler.CreateClientFactory());

        var action = () => tokenHandler.Login();

        var thrown = (await action.Should().ThrowAsync<InvalidCredentialException>()).Which;
        thrown.Message.Should().Be("Credential are invalid for username 'user'.");
    }

    [Test]
    public async Task LoginWithExternalSystemAndNoResponseDoesNotLeakApiKey()
    {
        configuration.UseExternalSystemLogin = true;
        configuration.UserId = ApiKey;
        var handler = new TestHttpMessageHandler();
        handler.EnqueueTimeout();
        var tokenHandler = new RefreshTokenHandler(configuration, accessInformation, noRetryPolicy, handler.CreateClientFactory());

        var action = () => tokenHandler.Login();

        var thrown = (await action.Should().ThrowAsync<NoResponseApiException>()).Which;
        AssertDoesNotLeak(thrown, ApiKey);
    }

    [Test]
    public async Task LoginWithExternalSystemAndInternalErrorDoesNotLeakApiKey()
    {
        configuration.UseExternalSystemLogin = true;
        configuration.UserId = ApiKey;
        const string serverResponseBody = "Server exploded";
        var handler = new TestHttpMessageHandler();
        handler.EnqueueResponse(HttpStatusCode.InternalServerError, serverResponseBody);
        var tokenHandler = new RefreshTokenHandler(configuration, accessInformation, noRetryPolicy, handler.CreateClientFactory());

        var action = () => tokenHandler.Login();

        var thrown = (await action.Should().ThrowAsync<InternalErrorApiException>()).Which;
        thrown.Message.Should().Be(LoginProcessErrorMessage);
        thrown.Body.Should().BeNull();
        AssertDoesNotLeak(thrown, ApiKey);
        (await thrown.GetResponseStringAsync()).Should().Be(serverResponseBody);
    }

    [Test]
    public async Task LoginWithUserPasswordAndInternalErrorDoesNotLeakPassword()
    {
        configuration.UseExternalSystemLogin = false;
        configuration.UserId = "user";
        configuration.Password = Password;
        const string serverResponseBody = "Server exploded";
        var handler = new TestHttpMessageHandler();
        handler.EnqueueResponse(HttpStatusCode.InternalServerError, serverResponseBody);
        var tokenHandler = new RefreshTokenHandler(configuration, accessInformation, noRetryPolicy, handler.CreateClientFactory());

        var action = () => tokenHandler.Login();

        var thrown = (await action.Should().ThrowAsync<InternalErrorApiException>()).Which;
        thrown.Message.Should().Be(LoginProcessErrorMessage);
        thrown.Body.Should().BeNull();
        AssertDoesNotLeak(thrown, Password);
        (await thrown.GetResponseStringAsync()).Should().Be(serverResponseBody);
    }

    private static void AssertDoesNotLeak(Exception thrown, string sensitiveValue)
    {
        thrown.InnerException.Should().BeOfType<ApiHttpException>();

        for (var current = thrown; current is not null; current = current.InnerException)
        {
            current.Should().NotBeOfType<FlurlHttpException>();
            current.Message.Should().NotContain(sensitiveValue);
            current.ToString().Should().NotContain(sensitiveValue);

            if (current is ApiClientException apiClientException)
                (apiClientException.Body ?? string.Empty).Should().NotContain(sensitiveValue);

            if (current is ApiHttpException apiHttpException)
            {
                (apiHttpException.ResponseBody ?? string.Empty).Should().NotContain(sensitiveValue);
                (apiHttpException.RequestUri ?? string.Empty).Should().NotContain(sensitiveValue);
            }
        }
    }
}
