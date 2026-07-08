using System;
using System.Net.Http;
using System.Threading.Tasks;
using Cauca.ApiClient.Extensions;
using Cauca.ApiClient.Tests.Helpers;
using Cauca.ApiClient.Tests.Mocks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using NUnit.Framework;

namespace Cauca.ApiClient.Tests.Services;

[TestFixture]
public class CaucaExternalSystemAuthExtensionsTests
{
    private sealed class ProbeClient(HttpClient httpClient)
    {
        public Task<HttpResponseMessage> CallAsync() => httpClient.GetAsync("mock");
    }

    [Test]
    public async Task AddCaucaExternalSystemAuth_AttachesHandlerThatLogsInAndCallsApi()
    {
        var transport = new TestHttpMessageHandler();
        transport.EnqueueJsonResponse(new LoginResult { AuthorizationType = "Bearer", AccessToken = "AccessToken", RefreshToken = "RefreshToken" });
        transport.EnqueueJsonResponse(new MockResponse());

        var configuration = new MockConfiguration { ApiBaseUrl = "http://test/", UserId = "the-api-key", UseExternalSystemLogin = true };
        var services = new ServiceCollection();
        services
            .AddHttpClient<ProbeClient>(client => client.BaseAddress = new Uri("http://test/"))
            .AddCaucaExternalSystemAuth(configuration);

        services.ConfigureAll<HttpClientFactoryOptions>(options =>
        {
            options.HttpMessageHandlerBuilderActions.Add(builder =>
                builder.PrimaryHandler = new PassThroughHandler(transport));
        });

        using var provider = services.BuildServiceProvider();
        var probe = provider.GetRequiredService<ProbeClient>();

        var response = await probe.CallAsync();

        response.IsSuccessStatusCode.Should().BeTrue();
        transport.Requests.Should().HaveCount(2);
        transport.Requests[0].RequestUri.Should().Be("http://test/Authentication/logonforexternalsystem");
        transport.Requests[1].HasHeader("Authorization", "Bearer AccessToken").Should().BeTrue();
    }
}
