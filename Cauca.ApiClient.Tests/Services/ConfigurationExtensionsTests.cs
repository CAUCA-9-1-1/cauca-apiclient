using System;
using System.Net.Http;
using Cauca.ApiClient.Services;
using Cauca.ApiClient.Tests.Mocks;
using FluentAssertions;
using Flurl.Http;
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
        var client = new HttpClient();
        client.BaseAddress = new Uri("https://api.example.com");
        
        var request = new FlurlClient(client).AppendRequest(null, "tests");

        request.Url.ToString().Should().Be("https://api.example.com/tests");
    }

    [Test]
    public void WithPrefix_WhenGeneratingRequest_ShouldHavePrefix()
    {
        var client = new HttpClient();
        client.BaseAddress = new Uri("https://api.example.com");
     
        var request = new FlurlClient(client).AppendRequest("prefix", "tests");
        
        request.Url.ToString().Should().Be("https://api.example.com/prefix/tests");
    }
}
