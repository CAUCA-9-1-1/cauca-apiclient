using System;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;

namespace Cauca.ApiClient.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddClient<TClient>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Transient,
        string clientName = null)
        where TClient : class
    {
        return AddClientRegistration<TClient>(services, lifetime, clientName);
    }

    public static IServiceCollection AddSecureClient<TClient>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Transient,
        string clientName = null)
        where TClient : class
    {
        if (lifetime == ServiceLifetime.Singleton)
            throw new InvalidOperationException("Secure clients cannot be registered as singletons because they keep mutable access-token state per instance.");

        return AddClientRegistration<TClient>(services, lifetime, clientName);
    }

    private static IServiceCollection AddClientRegistration<TClient>(
        IServiceCollection services,
        ServiceLifetime lifetime,
        string clientName)
        where TClient : class
    {
        var resolvedClientName = clientName ?? typeof(TClient).FullName ?? typeof(TClient).Name;

        services.AddHttpClient(resolvedClientName, static client => client.Timeout = Timeout.InfiniteTimeSpan);
        services.Add(new ServiceDescriptor(
            typeof(TClient),
            serviceProvider => ActivatorUtilities.CreateInstance<TClient>(
                serviceProvider,
                BuildAdditionalArguments<TClient>(serviceProvider, resolvedClientName)),
            lifetime));

        return services;
    }

    private static object[] BuildAdditionalArguments<TClient>(IServiceProvider serviceProvider, string clientName)
    {
        var clientFactoryArgument = CreateClientFactory(serviceProvider, clientName);
        var supportsHttpClientFactory = typeof(TClient)
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Any(constructor => constructor.GetParameters().Any(parameter => parameter.ParameterType == typeof(Func<HttpClient>)));

        return supportsHttpClientFactory ? [clientFactoryArgument] : [];
    }

    private static Func<HttpClient> CreateClientFactory(IServiceProvider serviceProvider, string clientName)
    {
        return () => serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient(clientName);
    }
}