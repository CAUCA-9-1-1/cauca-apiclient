using System;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Cauca.ApiClient.Exceptions;
using Cauca.ApiClient.Tests.Helpers;
using Cauca.ApiClient.Tests.Mocks;
using FluentAssertions;
using NUnit.Framework;

namespace Cauca.ApiClient.Tests.Services;

[TestFixture]
public class FluentTimeoutTests
{
    private static readonly TimeSpan StalledBodyDuration = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan MaximumAcceptedDuration = TimeSpan.FromSeconds(4);

    private MockConfiguration configuration;
    private TestHttpMessageHandler handler;
    private MockRepository repo;

    [SetUp]
    public void SetupTest()
    {
        configuration = new MockConfiguration
        {
            ApiBaseUrl = "http://test/",
            RequestTimeoutInSeconds = 1
        };
        handler = new TestHttpMessageHandler();
        handler.EnqueueStalledBodyResponse(StalledBodyDuration);
        repo = new MockRepository(configuration, handler.CreateClientFactory());
    }

    [Test]
    public async Task Request_BodyStallsAfterHeaders_ThrowsNoResponseApiExceptionWithinTimeout()
    {
        var stopwatch = Stopwatch.StartNew();

        var action = () => repo.GetMockStringUsingShortcutAsync().WaitAsync(MaximumAcceptedDuration);

        await action.Should().ThrowAsync<NoResponseApiException>();
        stopwatch.Elapsed.Should().BeLessThan(MaximumAcceptedDuration);
    }

    [Test]
    public async Task Request_CallerCancelsWhileBodyStalls_ThrowsOperationCanceledException()
    {
        configuration.RequestTimeoutInSeconds = 60;
        using var cancellationSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        var action = () => repo.GetMockStringWithCancellationAsync(cancellationSource.Token).WaitAsync(MaximumAcceptedDuration);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    [Test]
    public async Task Request_SuccessStatusReceivedThenBodyStalls_IsNeverRetried()
    {
        var retryingRepo = new MockRepositoryWithRetries(configuration, new InstantRetryBuilder(), handler.CreateClientFactory());

        var action = () => retryingRepo.GetMockStringAsync().WaitAsync(MaximumAcceptedDuration);

        (await action.Should().ThrowAsync<ResponseBodyNotReceivedApiException>())
            .Which.StatusCode.Should().Be(HttpStatusCode.OK);
        handler.Requests.Should().ContainSingle();
    }

    [Test]
    public async Task Request_HeadersTimeOut_IsRetried()
    {
        var retryHandler = new TestHttpMessageHandler();
        retryHandler.EnqueueTimeout();
        var retryingRepo = new MockRepositoryWithRetries(configuration, new InstantRetryBuilder(), retryHandler.CreateClientFactory());

        await retryingRepo.GetMockStringAsync();

        retryHandler.Requests.Should().HaveCount(2);
    }
}
