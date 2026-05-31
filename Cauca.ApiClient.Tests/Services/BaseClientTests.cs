using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Cauca.ApiClient.Exceptions;
using Cauca.ApiClient.Tests.Helpers;
using Cauca.ApiClient.Tests.Mocks;
using FluentAssertions;
using NUnit.Framework;

namespace Cauca.ApiClient.Tests.Services;

public class BaseClientTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private MockConfiguration configuration;
    private MockConfiguration configurationWithNoTrailingSlash;

    [SetUp]
    public void SetupTest()
    {
        configuration = new MockConfiguration
        {
            ApiBaseUrl = "http://test/"
        };

        configurationWithNoTrailingSlash = new MockConfiguration
        {
            ApiBaseUrl = "http://test"
        };
    }

    [Test]
    public async Task PostRequestAreCorrectlyExecuted()
    {
        var handler = new TestHttpMessageHandler();
        var entity = new MockEntity();
        handler.EnqueueJsonResponse(new MockResponse());
        var repo = CreateRepository(handler);

        await repo.PostMockAsync(entity);

        var request = handler.Requests.Should().ContainSingle().Which;
        request.RequestUri.Should().Be("http://test/mock");
        request.Method.Should().Be(HttpMethod.Post);
        request.Body.Should().Be(JsonSerializer.Serialize(entity, SerializerOptions));
    }

    [Test]
    public async Task PostRequestAreCorrectlyExecutedWhenBaseUrlDoesntEndWithSlash()
    {
        var handler = new TestHttpMessageHandler();
        handler.EnqueueJsonResponse(new MockResponse());
        var repo = CreateRepository(handler, configurationWithNoTrailingSlash);

        await repo.PostMockAsync(new MockEntity());

        handler.Requests.Should().ContainSingle().Which.RequestUri.Should().Be("http://test/mock");
    }

    [Test]
    public async Task RequestBuilder_WhenQueryParametersProvided_ShouldAppendThem()
    {
        var handler = new TestHttpMessageHandler();
        handler.EnqueueResponse(body: "Allo");
        var repo = CreateRepository(handler);

        _ = await repo.GetMockStringWithPageAndFilterAsync();

        handler.Requests.Should().ContainSingle().Which.RequestUri.Should().ContainAll("http://test/mock?", "page=3", "filter=open");
    }

    [Test]
    public async Task RequestBuilder_WhenHeadersProvided_ShouldAddThem()
    {
        var handler = new TestHttpMessageHandler();
        handler.EnqueueJsonResponse(new MockResponse());
        var repo = CreateRepository(handler);

        _ = await repo.PostMockWithHeadersAsync(new MockEntity());

        var request = handler.Requests.Should().ContainSingle().Which;
        request.HasHeader("X-Test", "one").Should().BeTrue();
        request.HasHeader("Another", "two").Should().BeTrue();
    }

    [Test]
    public async Task RequestBuilder_WhenSegmentsProvided_ShouldAppendThem()
    {
        var handler = new TestHttpMessageHandler();
        handler.EnqueueResponse(body: "Allo");
        var repo = CreateRepository(handler);

        _ = await repo.GetGeographyCitiesAsync();

        handler.Requests.Should().ContainSingle().Which.RequestUri.Should().Be("http://test/geography/10/cities");
    }

    [Test]
    public async Task RequestIsThrowingErrorWhenUrlIsNotFound()
    {
        var handler = new TestHttpMessageHandler();
        handler.EnqueueJsonResponse(new MockResponse(), HttpStatusCode.NotFound);
        var repo = CreateRepository(handler);

        var action = () => repo.PostMockAsync(new MockEntity());

        await action.Should().ThrowAsync<NotFoundApiException>();
    }

    [Test]
    public async Task RequestIsThrowingErrorWhenGettingBadParameters()
    {
        var handler = new TestHttpMessageHandler();
        handler.EnqueueJsonResponse(new MockResponse(), HttpStatusCode.BadRequest);
        var repo = CreateRepository(handler);

        var action = () => repo.PostMockAsync(new MockEntity());

        await action.Should().ThrowAsync<BadParameterApiException>();
    }

    [Test]
    public async Task InvalidRequest_WhenGeneratingException_ShouldContainsBody()
    {
        var expectedError = new { ErrorMessage = "Oh noes!" };
        var handler = new TestHttpMessageHandler();
        handler.EnqueueJsonResponse(expectedError, HttpStatusCode.BadRequest);
        var repo = CreateRepository(handler);

        var action = () => repo.PostMockAsync(new MockEntity());

        await action.Should().ThrowAsync<BadParameterApiException>()
            .Where(exception => exception.Body == JsonSerializer.Serialize(expectedError));
    }

    [Test]
    public async Task RequestIsThrowingErrorWhenGettingForbidden()
    {
        var handler = new TestHttpMessageHandler();
        handler.EnqueueJsonResponse(new MockResponse(), HttpStatusCode.Forbidden);
        var repo = CreateRepository(handler);

        var action = () => repo.PostMockAsync(new MockEntity());

        await action.Should().ThrowAsync<ForbiddenApiException>();
    }

    [Test]
    public async Task RequestIsThrowingErrorWhenGettingInternalError()
    {
        var handler = new TestHttpMessageHandler();
        handler.EnqueueJsonResponse(new MockResponse(), HttpStatusCode.InternalServerError);
        var repo = CreateRepository(handler);

        var action = () => repo.PostMockAsync(new MockEntity());

        await action.Should().ThrowAsync<InternalErrorApiException>();
    }

    [Test]
    public async Task RequestIsThrowingErrorWhenNotGettingAnAnswer()
    {
        var handler = new TestHttpMessageHandler();
        handler.EnqueueTimeout();
        var repo = CreateRepository(handler);

        var action = () => repo.PostMockAsync(new MockEntity());

        await action.Should().ThrowAsync<NoResponseApiException>();
    }

    [Test]
    public async Task BooleanAreCorrectlyReceived()
    {
        var handler = new TestHttpMessageHandler();
        handler.EnqueueResponse(body: true.ToString());
        var repo = CreateRepository(handler);

        var response = await repo.GetMockBooleanAsync();

        response.Should().BeTrue();
    }

    [Test]
    public async Task FalseBooleanAreCorrectlyReceived()
    {
        var handler = new TestHttpMessageHandler();
        handler.EnqueueResponse(body: false.ToString());
        var repo = CreateRepository(handler);

        var response = await repo.GetMockBooleanAsync();

        response.Should().BeFalse();
    }

    [Test]
    public async Task StringAreCorrectlyReceived()
    {
        var handler = new TestHttpMessageHandler();
        handler.EnqueueResponse(body: "Allo");
        var repo = CreateRepository(handler);

        var response = await repo.GetMockStringAsync();

        response.Should().Be("Allo");
    }

    [Test]
    public async Task StringAreCorrectlyReceivedWhenUsingGetAsyncString()
    {
        var handler = new TestHttpMessageHandler();
        handler.EnqueueResponse(body: "Allo");
        var repo = CreateRepository(handler);

        var response = await repo.GetMockStringUsingShortcutAsync();

        response.Should().Be("Allo");
    }

    [Test]
    public async Task IntAreCorrectlyReceived()
    {
        var handler = new TestHttpMessageHandler();
        handler.EnqueueResponse(body: "33");
        var repo = CreateRepository(handler);

        var response = await repo.GetMockIntAsync();

        response.Should().Be(33);
    }

    [Test]
    public async Task BytesArrayAreCorrectlyReceived()
    {
        const string text = "Ceci est mon test";
        var handler = new TestHttpMessageHandler();
        handler.EnqueueResponse(body: text);
        var repo = CreateRepository(handler);

        var response = await repo.GetMockBytesAsync();

        Encoding.UTF8.GetString(response).Should().Be(text);
    }

    [Test]
    public async Task DeleteWithBody_WhenCalling_ShouldCorrectlyCallApiWithBody()
    {
        var handler = new TestHttpMessageHandler();
        var body = new MockEntity();
        handler.EnqueueResponse();
        var repo = CreateRepository(handler);

        await repo.DeleteMockAsync(body);

        var request = handler.Requests.Should().ContainSingle().Which;
        request.Method.Should().Be(HttpMethod.Delete);
        request.RequestUri.Should().Be("http://test/mock");
        request.Body.Should().Be(JsonSerializer.Serialize(body, SerializerOptions));
    }

    private MockRepository CreateRepository(TestHttpMessageHandler handler, MockConfiguration currentConfiguration = null, string apiPrefix = null)
    {
        return new MockRepository(currentConfiguration ?? configuration, handler.CreateClientFactory(), apiPrefix);
    }
}
