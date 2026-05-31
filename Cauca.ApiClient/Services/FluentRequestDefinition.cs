using System;
using System.Collections.Generic;
using System.Globalization;

namespace Cauca.ApiClient.Services;

internal sealed class FluentRequestDefinition
{
    public FluentRequestDefinition(string route)
    {
        Route = route;
    }

    public string Route { get; }

    public IList<string> Segments { get; } = new List<string>();

    public IDictionary<string, object> QueryParameters { get; } = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

    public IDictionary<string, string> Headers { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public object Body { get; private set; }

    public Action<MultipartFormDataBuilder> MultipartContentBuilder { get; private set; }

    public void AppendSegments(IEnumerable<object> segments)
    {
        foreach (var segment in segments)
        {
            if (segment is null)
                continue;

            Segments.Add(Convert.ToString(segment, CultureInfo.InvariantCulture));
        }
    }

    public void MergeQueryParameters(object queryParameters)
    {
        foreach (var (key, value) in RequestUriBuilder.EnumerateParameters(queryParameters))
        {
            if (string.IsNullOrWhiteSpace(key))
                continue;

            QueryParameters[key] = value;
        }
    }

    public void MergeHeaders(object headers)
    {
        foreach (var (key, value) in RequestUriBuilder.EnumerateParameters(headers))
        {
            if (string.IsNullOrWhiteSpace(key) || value is null)
                continue;

            Headers[key] = Convert.ToString(value, CultureInfo.InvariantCulture);
        }
    }

    public void SetBody(object body)
    {
        Body = body;
        MultipartContentBuilder = null;
    }

    public void SetMultipart(Action<MultipartFormDataBuilder> configureMultipart)
    {
        MultipartContentBuilder = configureMultipart;
        Body = null;
    }

    public string BuildRoute()
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(Route))
            parts.Add(Route.Trim('/'));

        foreach (var segment in Segments)
            parts.Add(segment.Trim('/'));

        return string.Join("/", parts);
    }
}
