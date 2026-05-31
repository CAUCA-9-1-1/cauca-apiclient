using System;
using Cauca.ApiClient.Services;
using Cauca.ApiClient.Tests.Helpers;
using Cauca.ApiClient.Tests.Mocks;
using FluentAssertions;
using NUnit.Framework;

namespace Cauca.ApiClient.Tests.Services;
[TestFixture]
public class ConfigurationExtensionsTests
{
    [Test]
    public void NoApiPrefix_WhenGettingBaseUri_ShouldNotHavePrefix()
    {
        var configuration = new MockConfiguration
        {
            ApiBaseUrl = "https://api.example.com",
            ApiPrefix = null
        };

        var baseUrl = configuration.GetBaseUrl();

        baseUrl.Should().Be("https://api.example.com");
    }

    [Test]
    public void NoApiPrefixWithAuthenticationUrl_WhenGettingAuthenticationBaseUri_ShouldNotHavePrefix()
    {
        var configuration = new MockConfiguration
        {
            ApiBaseUrlForAuthentication = "https://auth.example.com",
            ApiPrefix = null
        };

        var baseUrl = configuration.GetAuthenticationBaseUrl();

        baseUrl.Should().Be("https://auth.example.com");
    }

    [Test]
    public void NoApiPrefixWithNoAuthenticationUrl_WhenGettingAuthenticationBaseUri_ShouldUseBaseUrl()
    {
        var configuration = new MockConfiguration
        {
            ApiBaseUrl = "https://api.example.com",
            ApiPrefix = null
        };
     
        var baseUrl = configuration.GetAuthenticationBaseUrl();
        
        baseUrl.Should().Be("https://api.example.com");
    }

    [Test]
    public void NoPrefix_WhenGeneratingRequest_ShouldNotHavePrefix()
    {
        var handler = new TestHttpMessageHandler();
        handler.EnqueueResponse();
        var configuration = new MockConfiguration { ApiBaseUrl = "https://api.example.com" };
        var repository = new MockRepository(configuration, handler.CreateClientFactory());

        _ = repository.GetStringAsync("tests").Result;

        handler.Requests.Should().ContainSingle().Which.RequestUri.Should().Be("https://api.example.com/tests");
    }

    [Test]
    public void WithPrefix_WhenGeneratingRequest_ShouldHavePrefix()
    {
        var handler = new TestHttpMessageHandler();
        handler.EnqueueResponse();
        var configuration = new MockConfiguration { ApiBaseUrl = "https://api.example.com" };
        var repository = new MockRepository(configuration, handler.CreateClientFactory(), "prefix");

        _ = repository.GetStringAsync("tests").Result;
        
        handler.Requests.Should().ContainSingle().Which.RequestUri.Should().Be("https://api.example.com/prefix/tests");
    }
}
