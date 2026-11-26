using System;
using System.Threading.Tasks;
using Cauca.ApiClient.Tests.Helpers;
using Cauca.ApiClient.Tests.Mocks;
using FluentAssertions;
using NUnit.Framework;

namespace Cauca.ApiClient.Tests.Services;

[TestFixture]
public class FluentRequestUriTests
{
    private MockConfiguration configuration;
    private TestHttpMessageHandler handler;
    private MockRepository repo;

    [SetUp]
    public void SetupTest()
    {
        configuration = new MockConfiguration { ApiBaseUrl = "http://test/" };
        handler = new TestHttpMessageHandler();
        repo = new MockRepository(configuration, handler.CreateClientFactory());
    }

    [Test]
    public async Task Request_RouteContainsQuery_KeepsRouteQuery()
    {
        await repo.GetStringAsync("orders?status=open");

        handler.Requests.Should().ContainSingle().Which.Uri.Query.Should().Contain("status=open");
    }

    [Test]
    public async Task Request_RouteContainsQueryAndBuilderAddsParameter_SendsBoth()
    {
        await repo.GetStringWithQueryParameterAsync("orders?status=open", "page", 2);

        var uri = handler.Requests.Should().ContainSingle().Which.Uri;
        uri.AbsolutePath.Should().Be("/orders");
        uri.Query.Should().Contain("status=open").And.Contain("page=2");
    }

    [Test]
    public async Task Request_BuilderParameterHasSameNameAsRouteParameter_BuilderValueWins()
    {
        await repo.GetStringWithQueryParameterAsync("orders?status=open&page=1", "PAGE", 2);

        var query = handler.Requests.Should().ContainSingle().Which.Uri.Query;
        query.Should().Contain("status=open").And.Contain("PAGE=2").And.NotContain("page=1");
    }

    [Test]
    public async Task Request_BuilderParameterIsNullAndRouteHasSameName_RemovesRouteParameter()
    {
        await repo.GetStringWithQueryParameterAsync("orders?status=open&page=1", "page", null);

        handler.Requests.Should().ContainSingle().Which.Uri.Query.Should().Be("?status=open");
    }

    [Test]
    public async Task AppendSegment_ValueContainsQuestionMarkAndHash_KeepsValueInPath()
    {
        await repo.GetStringWithSegmentAsync("orders", "a?c#d");

        var uri = handler.Requests.Should().ContainSingle().Which.Uri;
        uri.AbsolutePath.Should().Be("/orders/a%3Fc%23d");
        uri.Query.Should().BeEmpty();
        uri.Fragment.Should().BeEmpty();
    }

    [Test]
    public async Task AppendSegment_ValueContainsSlash_EncodesSlashInsideSingleSegment()
    {
        await repo.GetStringWithSegmentAsync("orders", "a/b");

        handler.Requests.Should().ContainSingle().Which.Uri.AbsolutePath.Should().Be("/orders/a%2Fb");
    }

    [Test]
    public async Task AppendSegment_RouteContainsQuery_PlacesSegmentInPathBeforeQuery()
    {
        await repo.GetStringWithSegmentAsync("orders?status=open", 5);

        var uri = handler.Requests.Should().ContainSingle().Which.Uri;
        uri.AbsolutePath.Should().Be("/orders/5");
        uri.Query.Should().Be("?status=open");
    }

    [Test]
    public async Task AppendSegment_ValueHasLeadingAndTrailingSlashes_TrimsThemBeforeEscaping()
    {
        await repo.GetStringWithSegmentAsync("orders", "/details/");

        handler.Requests.Should().ContainSingle().Which.Uri.AbsolutePath.Should().Be("/orders/details");
    }

    [TestCase(".")]
    [TestCase("..")]
    [TestCase("/..")]
    public async Task AppendSegment_ValueNavigatesThePath_ThrowsArgumentException(string segment)
    {
        var action = () => repo.GetStringWithSegmentAsync("orders", segment);

        await action.Should().ThrowAsync<ArgumentException>().WithMessage($"*'{segment}'*");
    }

    [TestCase("...")]
    [TestCase("a..b")]
    public async Task AppendSegment_ValueContainsDotsButIsNotNavigation_IsAccepted(string segment)
    {
        await repo.GetStringWithSegmentAsync("orders", segment);

        handler.Requests.Should().ContainSingle().Which.Uri.AbsolutePath.Should().Be($"/orders/{segment}");
    }
}
