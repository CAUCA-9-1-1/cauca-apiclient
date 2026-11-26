using Cauca.ApiClient.Services;
using FluentAssertions;
using NUnit.Framework;

namespace Cauca.ApiClient.Tests.Services;

[TestFixture]
public class RequestUriBuilderTests
{
    [Test]
    public void BuildUri_RouteContainsQueryAndNoParameters_KeepsRouteQuery()
    {
        var uri = RequestUriBuilder.BuildUri("http://test/", null, "orders?status=open");

        uri.AbsolutePath.Should().Be("/orders");
        uri.Query.Should().Be("?status=open");
    }

    [Test]
    public void BuildUri_RouteContainsQueryAndParameters_MergesBoth()
    {
        var uri = RequestUriBuilder.BuildUri("http://test/", null, "orders?status=open", new { page = 2 });

        uri.AbsolutePath.Should().Be("/orders");
        uri.Query.Should().Contain("status=open").And.Contain("page=2");
    }

    [Test]
    public void BuildUri_ParameterHasSameNameAsRouteParameterIgnoringCase_ReplacesRouteParameter()
    {
        var uri = RequestUriBuilder.BuildUri("http://test/", null, "orders?Page=1&status=open", new { page = 2 });

        uri.Query.Should().Contain("status=open").And.Contain("page=2").And.NotContain("Page=1");
    }

    [Test]
    public void BuildUri_RouteQueryValueEndsWithSlash_KeepsQueryAsWritten()
    {
        var uri = RequestUriBuilder.BuildUri("http://test/", null, "/files/?path=/");

        uri.AbsolutePath.Should().Be("/files");
        uri.Query.Should().Be("?path=/");
    }
}
