using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Cauca.ApiClient.Configuration;

namespace Cauca.ApiClient.Services;

internal static class RequestUriBuilder
{
    public static Uri BuildApiUri(IConfiguration configuration, string apiPrefix, string url, object queryParameters = null)
    {
        return BuildUri(configuration.GetBaseUrl(), apiPrefix, url, queryParameters);
    }

    public static Uri BuildAuthenticationUri(IConfiguration configuration, string apiPrefix, string url, object queryParameters = null)
    {
        return BuildUri(configuration.GetAuthenticationBaseUrl(), apiPrefix, url, queryParameters);
    }

    public static Uri BuildUri(string baseUrl, string apiPrefix, string url, object queryParameters = null)
    {
        var normalizedBaseUrl = (baseUrl ?? string.Empty).TrimEnd('/') + "/";
        var sanitizedPrefix = TrimPathSegment(apiPrefix);
        var sanitizedUrl = TrimPathSegment(url);
        var baseUri = string.IsNullOrWhiteSpace(sanitizedPrefix)
            ? new Uri(new Uri(normalizedBaseUrl, UriKind.Absolute), sanitizedUrl)
            : new Uri(new Uri(normalizedBaseUrl, UriKind.Absolute), $"{sanitizedPrefix}/{sanitizedUrl}");
        var uriBuilder = new UriBuilder(baseUri)
        {
            Query = BuildQueryString(queryParameters)
        };

        return uriBuilder.Uri;
    }

    private static string TrimPathSegment(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim('/');
    }

    private static string BuildQueryString(object queryParameters)
    {
        var segments = new List<string>();
        foreach (var (key, value) in EnumerateParameters(queryParameters))
        {
            if (string.IsNullOrWhiteSpace(key) || value is null)
                continue;

            segments.Add($"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(Convert.ToString(value, CultureInfo.InvariantCulture))}");
        }

        return string.Join("&", segments);
    }

    internal static IEnumerable<(string Key, object Value)> EnumerateParameters(object queryParameters)
    {
        if (queryParameters is null)
            yield break;

        if (queryParameters is IEnumerable<KeyValuePair<string, string>> stringPairs)
        {
            foreach (var pair in stringPairs)
                yield return (pair.Key, pair.Value);
            yield break;
        }

        if (queryParameters is IEnumerable<KeyValuePair<string, object>> objectPairs)
        {
            foreach (var pair in objectPairs)
                yield return (pair.Key, pair.Value);
            yield break;
        }

        if (queryParameters is IDictionary dictionary)
        {
            foreach (DictionaryEntry entry in dictionary)
                yield return (Convert.ToString(entry.Key, CultureInfo.InvariantCulture), entry.Value);
            yield break;
        }

        foreach (var property in queryParameters.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
            yield return (property.Name, property.GetValue(queryParameters));
    }
}