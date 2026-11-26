using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
        var sanitizedUrl = TrimRoute(url);
        var baseUri = string.IsNullOrWhiteSpace(sanitizedPrefix)
            ? new Uri(new Uri(normalizedBaseUrl, UriKind.Absolute), sanitizedUrl)
            : new Uri(new Uri(normalizedBaseUrl, UriKind.Absolute), $"{sanitizedPrefix}/{sanitizedUrl}");
        var parameters = EnumerateParameters(queryParameters).ToList();
        var uriBuilder = new UriBuilder(baseUri)
        {
            Query = MergeQueries(baseUri.Query, parameters)
        };

        return uriBuilder.Uri;
    }

    private static string MergeQueries(string routeQuery, List<(string Key, object Value)> parameters)
    {
        var overriddenKeys = parameters
            .Where(parameter => !string.IsNullOrWhiteSpace(parameter.Key))
            .Select(parameter => parameter.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var keptRouteParts = routeQuery.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Where(part => !overriddenKeys.Contains(GetUnescapedKey(part)));

        return string.Join("&", keptRouteParts.Concat(BuildQueryParts(parameters)));
    }

    private static string GetUnescapedKey(string queryPart)
    {
        var separatorIndex = queryPart.IndexOf('=');
        var rawKey = separatorIndex < 0 ? queryPart : queryPart[..separatorIndex];
        return Uri.UnescapeDataString(rawKey);
    }

    private static string TrimRoute(string route)
    {
        if (string.IsNullOrWhiteSpace(route))
            return string.Empty;

        var queryIndex = route.IndexOf('?');
        return queryIndex < 0
            ? TrimPathSegment(route)
            : TrimPathSegment(route[..queryIndex]) + route[queryIndex..];
    }

    private static string TrimPathSegment(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim('/');
    }

    private static IEnumerable<string> BuildQueryParts(IEnumerable<(string Key, object Value)> parameters)
    {
        foreach (var (key, value) in parameters)
        {
            if (string.IsNullOrWhiteSpace(key) || value is null)
                continue;

            yield return $"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(Convert.ToString(value, CultureInfo.InvariantCulture))}";
        }
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