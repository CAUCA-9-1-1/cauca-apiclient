using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Cauca.ApiClient.Exceptions;
using Cauca.ApiClient.Extensions;
using Cauca.ApiClient.Tests.Helpers;
using Cauca.ApiClient.Tests.Mocks;
using FluentAssertions;
using NUnit.Framework;

namespace Cauca.ApiClient.Tests.Services;

[TestFixture]
public class BaseSecureClientTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private MockConfiguration configuration;

    [SetUp]
    public void SetupTest()
    {
        configuration = new MockConfiguration
        {
            ApiBaseUrl = "http://test/",
            UserId = "user",
            Password = "password"
        };
    }

    [Test]
    public async Task RequestIsCorrectlyExecuted()
    {
        var handler = new TestHttpMessageHandler();
        handler.EnqueueJsonResponse(new MockResponse());
        var entity = new MockEntity();
        var repo = CreateSecureRepository(handler, isLoggedIn: true);

        await repo.PostMockAsync(entity);

        var request = handler.Requests.Should().ContainSingle().Which;
        request.RequestUri.Should().Be("http://test/mock");
        request.Method.Should().Be(HttpMethod.Post);
        request.Body.Should().Be(JsonSerializer.Serialize(entity, SerializerOptions));
        request.HasHeader("Authorization", "Mock Token").Should().BeTrue();
    }

    [Test]
    public async Task RequestLoginBeforeExecutingWhenNotLoggedIn()
    {
        var handler = new TestHttpMessageHandler();
        handler.EnqueueJsonResponse(new LoginResult { AuthorizationType = "Bearer", RefreshToken = "NewRefreshToken", AccessToken = "NewAccessToken" });
        handler.EnqueueJsonResponse(new MockResponse());
        var repo = CreateSecureRepository(handler);

        await repo.PostMockAsync(new MockEntity());

        handler.Requests.Should().HaveCount(2);
        handler.Requests[0].RequestUri.Should().Be("http://test/Authentication/logon");
        handler.Requests[1].RequestUri.Should().Be("http://test/mock");
        handler.Requests[1].HasHeader("Authorization", "Bearer NewAccessToken").Should().BeTrue();
    }

    [Test]
    public async Task WithApiBaseUrlForAuthentication_RequestLoginBeforeExecutingWhenNotLoggedIn_ShouldBeExecutedWithUrlForAuthentication()
    {
        configuration.ApiBaseUrlForAuthentication = "http://test-for-authentication";
        var handler = new TestHttpMessageHandler();
        handler.EnqueueJsonResponse(new LoginResult { AuthorizationType = "Bearer", RefreshToken = "NewRefreshToken", AccessToken = "NewAccessToken" });
        handler.EnqueueJsonResponse(new MockResponse());
        var repo = CreateSecureRepository(handler);

        await repo.PostMockAsync(new MockEntity());

        handler.Requests[0].RequestUri.Should().Be("http://test-for-authentication/Authentication/logon");
        handler.Requests[1].RequestUri.Should().Be("http://test/mock");
    }

    [Test]
    public async Task RequestBuilder_ShouldSupportSegmentsQueryAndHeadersOnSecureRequests()
    {
        var handler = new TestHttpMessageHandler();
        handler.EnqueueJsonResponse(new MockResponse());
        var repo = CreateSecureRepository(handler, isLoggedIn: true);

        await repo.GetGeographyCitiesWithQueryAndHeadersAsync();

        var request = handler.Requests.Should().ContainSingle().Which;
        request.RequestUri.Should().ContainAll("http://test/geography/10/cities?", "Top=20");
        request.HasHeader("Extra", "value").Should().BeTrue();
        request.HasHeader("Authorization", "Mock Token").Should().BeTrue();
    }

    [Test]
    public async Task LoginCorrectlySetAccessAndRefreshToken()
    {
        var handler = new TestHttpMessageHandler();
        handler.EnqueueJsonResponse(new LoginResult { AuthorizationType = "Bearer", RefreshToken = "NewRefreshToken", AccessToken = "NewAccessToken" });
        handler.EnqueueJsonResponse(new MockResponse());
        var repo = CreateSecureRepository(handler);

        await repo.PostMockAsync(new MockEntity());

        repo.AccessInformation.RefreshToken.Should().Be("NewRefreshToken");
        repo.AccessInformation.AccessToken.Should().Be("NewAccessToken");
    }

    [Test]
    public async Task RequestRefreshTokenThenRetryWhenItsExpired()
    {
        var handler = new TestHttpMessageHandler();
        handler.EnqueueJsonResponse(new MockResponse(), HttpStatusCode.Unauthorized, (RestResponseExtensions.AccessTokenExpired, "True"));
        handler.EnqueueJsonResponse(new TokenRefreshResult { AccessToken = "NewToken" });
        handler.EnqueueJsonResponse(new MockResponse());
        var repo = CreateSecureRepository(handler, isLoggedIn: true);

        await repo.PostMockAsync(new MockEntity());

        handler.Requests.Should().HaveCount(3);
        handler.Requests[0].RequestUri.Should().Be("http://test/mock");
        handler.Requests[1].RequestUri.Should().Be("http://test/Authentication/refresh");
        handler.Requests[2].RequestUri.Should().Be("http://test/mock");
        handler.Requests[2].HasHeader("Authorization", "Mock NewToken").Should().BeTrue();
    }

    [Test]
    public async Task RequestLogBackInWhenRefreshTokenAndAccessTokenAreExpired()
    {
        var loginResult = new LoginResult { AuthorizationType = "Bearer", RefreshToken = "NewRefreshToken", AccessToken = "NewAccessToken" };
        var handler = new TestHttpMessageHandler();
        handler.EnqueueJsonResponse(new MockResponse(), HttpStatusCode.Unauthorized, (RestResponseExtensions.AccessTokenExpired, "True"));
        handler.EnqueueJsonResponse(new TokenRefreshResult(), HttpStatusCode.Unauthorized, (RestResponseExtensions.RefreshTokenExpired, "True"));
        handler.EnqueueJsonResponse(loginResult);
        handler.EnqueueJsonResponse(new MockResponse());
        var repo = CreateSecureRepository(handler, isLoggedIn: true);

        await repo.PostMockAsync(new MockEntity());

        handler.Requests.Should().HaveCount(4);
        handler.Requests[0].RequestUri.Should().Be("http://test/mock");
        handler.Requests[1].RequestUri.Should().Be("http://test/Authentication/refresh");
        handler.Requests[2].RequestUri.Should().Be("http://test/Authentication/logon");
        handler.Requests[3].RequestUri.Should().Be("http://test/mock");
        handler.Requests[3].HasHeader("Authorization", "Bearer NewAccessToken").Should().BeTrue();
    }

    [Test]
    public async Task RequestIsThrowingErrorWhenUrlIsNotFound()
    {
        var handler = new TestHttpMessageHandler();
        handler.EnqueueJsonResponse(new MockResponse(), HttpStatusCode.NotFound);
        var repo = CreateSecureRepository(handler, isLoggedIn: true);

        var action = () => repo.PostMockAsync(new MockEntity());

        await action.Should().ThrowAsync<NotFoundApiException>();
    }

    [Test]
    public async Task RequestIsThrowingErrorWhenGettingBadParameters()
    {
        var handler = new TestHttpMessageHandler();
        handler.EnqueueJsonResponse(new MockResponse(), HttpStatusCode.BadRequest);
        var repo = CreateSecureRepository(handler, isLoggedIn: true);

        var action = () => repo.PostMockAsync(new MockEntity());

        await action.Should().ThrowAsync<BadParameterApiException>();
    }

    [Test]
    public async Task RequestIsThrowingErrorWhenGettingForbidden()
    {
        var handler = new TestHttpMessageHandler();
        handler.EnqueueJsonResponse(new MockResponse(), HttpStatusCode.Forbidden);
        var repo = CreateSecureRepository(handler, isLoggedIn: true);

        var action = () => repo.PostMockAsync(new MockEntity());

        await action.Should().ThrowAsync<ForbiddenApiException>();
    }

    [Test]
    public async Task RequestIsThrowingErrorWhenGettingInternalError()
    {
        var handler = new TestHttpMessageHandler();
        handler.EnqueueJsonResponse(new MockResponse(), HttpStatusCode.InternalServerError);
        var repo = CreateSecureRepository(handler, isLoggedIn: true);

        var action = () => repo.PostMockAsync(new MockEntity());

        await action.Should().ThrowAsync<InternalErrorApiException>();
    }

    [Test]
    public async Task RequestIsThrowingErrorWhenNotGettingAnAnswer()
    {
        var handler = new TestHttpMessageHandler();
        handler.EnqueueTimeout();
        var repo = CreateSecureRepository(handler, isLoggedIn: true);

        var action = () => repo.PostMockAsync(new MockEntity());

        await action.Should().ThrowAsync<NoResponseApiException>();
    }

    private MockSecureRepository CreateSecureRepository(TestHttpMessageHandler handler, bool isLoggedIn = false)
    {
        var repo = new MockSecureRepository(configuration, handler.CreateClientFactory());
        if (isLoggedIn)
        {
            repo.AccessInformation.AccessToken = "Token";
            repo.AccessInformation.RefreshToken = "RefreshToken";
            repo.AccessInformation.AuthorizationType = "Mock";
        }

        return repo;
    }
}
