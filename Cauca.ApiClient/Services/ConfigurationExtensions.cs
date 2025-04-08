using Cauca.ApiClient.Configuration;
using Flurl.Http;

namespace Cauca.ApiClient.Services;

public static class ConfigurationExtensions
{
    public static string GetAuthenticationBaseUrl(this IConfiguration configuration)
    {
        return configuration.ApiBaseUrlForAuthentication ?? configuration.ApiBaseUrl;
    }

    public static string GetBaseUrl(this IConfiguration configuration)
    {
        return configuration.ApiBaseUrl;
    }

    public static IFlurlRequest AppendRequest(this IFlurlClient request, string prefix, string url)
    {
        return string.IsNullOrWhiteSpace(prefix) ? request.Request(url) : request
            .Request(prefix, url);
    }
}