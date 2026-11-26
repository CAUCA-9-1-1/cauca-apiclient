using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Cauca.ApiClient.Tests.Helpers;

public sealed class TestHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>> _responses = [];
    private readonly HttpClient _httpClient;

    public TestHttpMessageHandler()
    {
        _httpClient = new HttpClient(this, disposeHandler: false)
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
    }

    public List<RecordedRequest> Requests { get; } = [];

    public Func<HttpClient> CreateClientFactory()
    {
        return () => _httpClient;
    }

    public void EnqueueJsonResponse<T>(T body, HttpStatusCode statusCode = HttpStatusCode.OK, params (string Name, string Value)[] headers)
    {
        EnqueueResponse(statusCode, JsonSerializer.Serialize(body), "application/json", headers);
    }

    public void EnqueueResponse(HttpStatusCode statusCode = HttpStatusCode.OK, string body = null, string contentType = "text/plain", params (string Name, string Value)[] headers)
    {
        _responses.Enqueue((request, _) => Task.FromResult(CreateResponse(request, statusCode, body, contentType, headers)));
    }

    public void EnqueueStalledBodyResponse(TimeSpan stallDuration)
    {
        _responses.Enqueue((request, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            RequestMessage = request,
            Content = new StalledContent(stallDuration)
        }));
    }

    public void EnqueueBrokenBodyResponse(HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        _responses.Enqueue((request, _) => Task.FromResult(new HttpResponseMessage(statusCode)
        {
            RequestMessage = request,
            Content = new BrokenConnectionContent()
        }));
    }

    public void EnqueueTimeout()
    {
        _responses.Enqueue((_, cancellationToken) => Task.FromException<HttpResponseMessage>(new OperationCanceledException("Simulated timeout.", new TimeoutException(), cancellationToken)));
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(await RecordedRequest.CreateAsync(request, cancellationToken));

        if (_responses.Count == 0)
            return CreateResponse(request, HttpStatusCode.OK, null, null);

        return await _responses.Dequeue()(request, cancellationToken);
    }

    private static HttpResponseMessage CreateResponse(
        HttpRequestMessage request,
        HttpStatusCode statusCode,
        string body,
        string contentType,
        IEnumerable<(string Name, string Value)> headers = null)
    {
        var response = new HttpResponseMessage(statusCode)
        {
            RequestMessage = request
        };

        if (body is not null)
        {
            response.Content = new StringContent(body, Encoding.UTF8);
            if (!string.IsNullOrWhiteSpace(contentType))
                response.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        }

        foreach (var (name, value) in headers ?? [])
        {
            if (!response.Headers.TryAddWithoutValidation(name, value) && response.Content is not null)
                response.Content.Headers.TryAddWithoutValidation(name, value);
        }

        return response;
    }
}

public sealed class RecordedRequest
{
    private RecordedRequest(HttpMethod method, Uri uri, string body, IReadOnlyDictionary<string, string[]> headers)
    {
        Method = method;
        Uri = uri;
        RequestUri = uri?.ToString();
        Body = body;
        Headers = headers;
    }

    public HttpMethod Method { get; }

    public string RequestUri { get; }

    public Uri Uri { get; }

    public string Body { get; }

    public IReadOnlyDictionary<string, string[]> Headers { get; }

    public bool HasHeader(string name, string value = null)
    {
        return Headers.TryGetValue(name, out var values)
            && (value is null || values.Contains(value));
    }

    public static async Task<RecordedRequest> CreateAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string body = null;
        if (request.Content is not null)
        {
            await request.Content.LoadIntoBufferAsync();
            body = await request.Content.ReadAsStringAsync(cancellationToken);
        }

        var headers = request.Headers
            .Concat(request.Content?.Headers ?? Enumerable.Empty<KeyValuePair<string, IEnumerable<string>>>())
            .GroupBy(header => header.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.SelectMany(value => value.Value).ToArray(), StringComparer.OrdinalIgnoreCase);

        return new RecordedRequest(
            request.Method,
            request.RequestUri,
            body,
            headers);
    }
}

internal sealed class StalledContent : HttpContent
{
    private readonly TimeSpan _stallDuration;

    public StalledContent(TimeSpan stallDuration)
    {
        _stallDuration = stallDuration;
    }

    protected override Task SerializeToStreamAsync(Stream stream, TransportContext context)
    {
        return SerializeToStreamAsync(stream, context, CancellationToken.None);
    }

    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext context, CancellationToken cancellationToken)
    {
        await Task.Delay(_stallDuration, cancellationToken);
        await stream.WriteAsync(Encoding.UTF8.GetBytes("late"), cancellationToken);
    }

    protected override async Task<Stream> CreateContentReadStreamAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(_stallDuration, cancellationToken);
        return new MemoryStream(Encoding.UTF8.GetBytes("late"));
    }

    protected override bool TryComputeLength(out long length)
    {
        length = -1;
        return false;
    }
}

internal sealed class BrokenConnectionContent : HttpContent
{
    public BrokenConnectionContent()
    {
        Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");
    }

    protected override Task SerializeToStreamAsync(Stream stream, TransportContext context)
    {
        throw new IOException("Simulated connection cut.");
    }

    protected override Task<Stream> CreateContentReadStreamAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<Stream>(new BrokenStream());
    }

    protected override bool TryComputeLength(out long length)
    {
        length = -1;
        return false;
    }

    private sealed class BrokenStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new IOException("Simulated connection cut.");
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            throw new IOException("Simulated connection cut.");
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
