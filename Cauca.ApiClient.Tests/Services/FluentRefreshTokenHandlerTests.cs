using System.Net;
using System.Threading.Tasks;
using Cauca.ApiClient.Configuration;
using Cauca.ApiClient.Exceptions;
using Cauca.ApiClient.Services;
using Cauca.ApiClient.Tests.Helpers;
using Cauca.ApiClient.Tests.Mocks;
using FluentAssertions;
using NUnit.Framework;
using Polly;

namespace Cauca.ApiClient.Tests.Services;

[TestFixture]
public class FluentRefreshTokenHandlerTests
{
    private const string ApiKey = "secret-api-key-123";
    private const string Password = "secret-password-123";

    private MockConfiguration configuration;
    private IAsyncPolicy noRetryPolicy;
    private AccessInformation accessInformation;

    [SetUp]
    public void SetupTest()
    {
        noRetryPolicy = new InstantRetryBuilder().BuildRetryPolicy(0);
        accessInformation = new AccessInformation();
        configuration = new MockConfiguration
        {
            ApiBaseUrl = "http://test",
            UseExternalSystemLogin = false,
            UserId = "user",
            Password = "password"
        };
    }

    [Test]
    public async Task LoginWithExternalSystemAndUnauthorizedResponseThrowsInvalidCredentialExceptionWithoutApiKey()
    {
        configuration.UseExternalSystemLogin = true;
        configuration.UserId = ApiKey;
        var handler = new TestHttpMessageHandler();
        handler.EnqueueResponse(HttpStatusCode.Unauthorized);
        var tokenHandler = new FluentRefreshTokenHandler(configuration, accessInformation, noRetryPolicy, handler.CreateClientFactory());

        var action = () => tokenHandler.Login();

        (await action.Should().ThrowAsync<InvalidCredentialException>())
            .Which.Message.Should().Be("The configured external system credentials were rejected.");
    }

    [Test]
    public async Task LoginWithExternalSystemAndUnauthorizedResponseDoesNotLeakApiKey()
    {
        configuration.UseExternalSystemLogin = true;
        configuration.UserId = ApiKey;
        var handler = new TestHttpMessageHandler();
        handler.EnqueueResponse(HttpStatusCode.Unauthorized);
        var tokenHandler = new FluentRefreshTokenHandler(configuration, accessInformation, noRetryPolicy, handler.CreateClientFactory());

        var action = () => tokenHandler.Login();

        var thrown = (await action.Should().ThrowAsync<InvalidCredentialException>()).Which;
        thrown.ToString().Should().NotContain(ApiKey);
    }

    [Test]
    public async Task LoginWithUserPasswordAndUnauthorizedResponseStillContainsUsername()
    {
        configuration.UseExternalSystemLogin = false;
        configuration.UserId = "user";
        configuration.Password = Password;
        var handler = new TestHttpMessageHandler();
        handler.EnqueueResponse(HttpStatusCode.Unauthorized);
        var tokenHandler = new FluentRefreshTokenHandler(configuration, accessInformation, noRetryPolicy, handler.CreateClientFactory());

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
        var tokenHandler = new FluentRefreshTokenHandler(configuration, accessInformation, noRetryPolicy, handler.CreateClientFactory());

        var action = () => tokenHandler.Login();

        var thrown = (await action.Should().ThrowAsync<NoResponseApiException>()).Which;
        thrown.ToString().Should().NotContain(ApiKey);
    }

    [Test]
    public async Task LoginWithExternalSystemAndInternalErrorDoesNotLeakApiKey()
    {
        configuration.UseExternalSystemLogin = true;
        configuration.UserId = ApiKey;
        var handler = new TestHttpMessageHandler();
        handler.EnqueueResponse(HttpStatusCode.InternalServerError);
        var tokenHandler = new FluentRefreshTokenHandler(configuration, accessInformation, noRetryPolicy, handler.CreateClientFactory());

        var action = () => tokenHandler.Login();

        var thrown = (await action.Should().ThrowAsync<InternalErrorApiException>()).Which;
        thrown.ToString().Should().NotContain(ApiKey);
    }
}
