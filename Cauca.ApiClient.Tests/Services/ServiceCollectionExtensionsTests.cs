using System;
using Cauca.ApiClient.Extensions;
using Cauca.ApiClient.Tests.Mocks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace Cauca.ApiClient.Tests.Services;

[TestFixture]
public class ServiceCollectionExtensionsTests
{
    [Test]
    public void AddClient_WhenConfigurationIsRegistered_ShouldResolveClient()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new MockConfiguration { ApiBaseUrl = "http://test" });
        services.AddClient<MockRepository>();

        using var serviceProvider = services.BuildServiceProvider();
        var client = serviceProvider.GetRequiredService<MockRepository>();

        client.Should().NotBeNull();
    }

    [Test]
    public void AddSecureClient_WhenConfigurationIsRegistered_ShouldResolveClient()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new MockConfiguration { ApiBaseUrl = "http://test" });
        services.AddSecureClient<MockSecureRepository>(ServiceLifetime.Scoped);

        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<MockSecureRepository>();

        client.Should().NotBeNull();
    }

    [Test]
    public void AddSecureClient_WhenLifetimeIsSingleton_ShouldThrow()
    {
        var services = new ServiceCollection();

        var action = () => services.AddSecureClient<MockSecureRepository>(ServiceLifetime.Singleton);

        action.Should().Throw<InvalidOperationException>();
    }
}