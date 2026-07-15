using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Cauca.ApiClient.Services;

public sealed class FluentRequestBuilder
{
    private readonly IFluentRequestExecutor _executor;
    private readonly FluentRequestDefinition _request;

    internal FluentRequestBuilder(IFluentRequestExecutor executor, string route)
    {
        _executor = executor;
        _request = new FluentRequestDefinition(route);
    }

    public FluentRequestBuilder AppendSegment(object segment)
    {
        _request.AppendSegments([segment]);
        return this;
    }

    public FluentRequestBuilder AppendSegments(params object[] segments)
    {
        _request.AppendSegments(segments);
        return this;
    }

    public FluentRequestBuilder AddQueryParameter(string name, object value)
    {
        _request.MergeQueryParameters(new[] { new KeyValuePair<string, object>(name, value) });
        return this;
    }

    public FluentRequestBuilder WithQueryParameters(object queryParameters)
    {
        _request.MergeQueryParameters(queryParameters);
        return this;
    }

    public FluentRequestBuilder WithHeader(string name, string value)
    {
        _request.MergeHeaders(new[] { new KeyValuePair<string, object>(name, value) });
        return this;
    }

    public FluentRequestBuilder WithHeaders(object headers)
    {
        _request.MergeHeaders(headers);
        return this;
    }

    public FluentRequestBuilder WithBody(object body)
    {
        _request.SetBody(body);
        return this;
    }

    public Task GetAsync(CancellationToken cancellationToken = default)
    {
        return _executor.SendAsync(_request, HttpMethod.Get, cancellationToken);
    }

    public Task<TResult> GetAsync<TResult>(CancellationToken cancellationToken = default)
    {
        return _executor.SendAsync<TResult>(_request, HttpMethod.Get, cancellationToken);
    }

    public Task<string> GetStringAsync(CancellationToken cancellationToken = default)
    {
        return _executor.SendAsync<string>(_request, HttpMethod.Get, cancellationToken);
    }

    public Task<byte[]> GetBytesAsync(CancellationToken cancellationToken = default)
    {
        return _executor.SendAsync<byte[]>(_request, HttpMethod.Get, cancellationToken);
    }

    public Task<Stream> GetStreamAsync(CancellationToken cancellationToken = default)
    {
        return _executor.SendAsync<Stream>(_request, HttpMethod.Get, cancellationToken);
    }

    public Task PostAsync(CancellationToken cancellationToken = default)
    {
        return _executor.SendAsync(_request, HttpMethod.Post, cancellationToken);
    }

    public Task<TResult> PostAsync<TResult>(CancellationToken cancellationToken = default)
    {
        return _executor.SendAsync<TResult>(_request, HttpMethod.Post, cancellationToken);
    }

    public Task<byte[]> PostAndReceiveBytesAsync(CancellationToken cancellationToken = default)
    {
        return _executor.SendAsync<byte[]>(_request, HttpMethod.Post, cancellationToken);
    }

    public Task<Stream> PostAndReceiveStreamAsync(CancellationToken cancellationToken = default)
    {
        return _executor.SendAsync<Stream>(_request, HttpMethod.Post, cancellationToken);
    }

    public Task<TResult> PutAsync<TResult>(CancellationToken cancellationToken = default)
    {
        return _executor.SendAsync<TResult>(_request, HttpMethod.Put, cancellationToken);
    }

    public Task<TResult> PatchAsync<TResult>(CancellationToken cancellationToken = default)
    {
        return _executor.SendAsync<TResult>(_request, HttpMethod.Patch, cancellationToken);
    }

    public Task<TResult> DeleteAsync<TResult>(CancellationToken cancellationToken = default)
    {
        return _executor.SendAsync<TResult>(_request, HttpMethod.Delete, cancellationToken);
    }

    public Task DeleteAsync(CancellationToken cancellationToken = default)
    {
        return _executor.SendAsync(_request, HttpMethod.Delete, cancellationToken);
    }

    internal FluentRequestBuilder WithMultipart(Action<MultipartFormDataBuilder> configureMultipart)
    {
        _request.SetMultipart(configureMultipart);
        return this;
    }
}

internal interface IFluentRequestExecutor
{
    Task SendAsync(FluentRequestDefinition request, HttpMethod method, CancellationToken cancellationToken);

    Task<TResult> SendAsync<TResult>(FluentRequestDefinition request, HttpMethod method, CancellationToken cancellationToken);
}
