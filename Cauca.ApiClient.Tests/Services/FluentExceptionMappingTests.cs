using System.Net;
using System.Threading.Tasks;
using Cauca.ApiClient.Exceptions;
using Cauca.ApiClient.Extensions;
using Cauca.ApiClient.Tests.Helpers;
using Cauca.ApiClient.Tests.Mocks;
using FluentAssertions;
using NUnit.Framework;

namespace Cauca.ApiClient.Tests.Services;

[TestFixture]
public class FluentExceptionMappingTests
{
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
    public async Task SecureRequestWithResult_ResentRequestAfterRefreshReturnsNotFound_ThrowsNotFoundApiException()
    {
        var handler = new TestHttpMessageHandler();
        EnqueueExpiredAccessTokenThenRefreshThenNotFound(handler);
        var repo = CreateLoggedInSecureRepository(handler);

        var action = () => repo.GetMockStringAsync();

        await action.Should().ThrowAsync<NotFoundApiException>();
    }

    [Test]
    public async Task SecureRequestWithoutResult_ResentRequestAfterRefreshReturnsNotFound_ThrowsNotFoundApiException()
    {
        var handler = new TestHttpMessageHandler();
        EnqueueExpiredAccessTokenThenRefreshThenNotFound(handler);
        var repo = CreateLoggedInSecureRepository(handler);

        var action = () => repo.DeleteMockAsync();

        await action.Should().ThrowAsync<NotFoundApiException>();
    }

    [TestCase(HttpStatusCode.Conflict)]
    [TestCase(HttpStatusCode.UnprocessableEntity)]
    public async Task Request_UnmappedStatusCodeWithAnswer_ThrowsApiClientException(HttpStatusCode statusCode)
    {
        var handler = new TestHttpMessageHandler();
        handler.EnqueueJsonResponse(new MockResponse(), statusCode);
        var repo = new MockRepository(configuration, handler.CreateClientFactory());

        var action = () => repo.PostMockAsync(new MockEntity());

        await action.Should().ThrowAsync<ApiClientException>();
    }

    [TestCase(HttpStatusCode.Conflict)]
    [TestCase(HttpStatusCode.UnprocessableEntity)]
    public async Task Request_UnmappedStatusCodeWithAnswer_ExposesStatusCode(HttpStatusCode statusCode)
    {
        var handler = new TestHttpMessageHandler();
        handler.EnqueueJsonResponse(new MockResponse(), statusCode);
        var repo = new MockRepository(configuration, handler.CreateClientFactory());

        var action = () => repo.PostMockAsync(new MockEntity());

        (await action.Should().ThrowAsync<UnexpectedStatusCodeApiException>())
            .Which.StatusCode.Should().Be(statusCode);
    }

    private static void EnqueueExpiredAccessTokenThenRefreshThenNotFound(TestHttpMessageHandler handler)
    {
        handler.EnqueueJsonResponse(new MockResponse(), HttpStatusCode.Unauthorized, (RestResponseExtensions.AccessTokenExpired, "True"));
        handler.EnqueueJsonResponse(new TokenRefreshResult { AccessToken = "NewToken" });
        handler.EnqueueJsonResponse(new MockResponse(), HttpStatusCode.NotFound);
    }

    private MockSecureRepository CreateLoggedInSecureRepository(TestHttpMessageHandler handler)
    {
        var repo = new MockSecureRepository(configuration, handler.CreateClientFactory());
        repo.AccessInformation.AccessToken = "Token";
        repo.AccessInformation.RefreshToken = "RefreshToken";
        repo.AccessInformation.AuthorizationType = "Mock";
        return repo;
    }
}
