using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Cauca.ApiClient.Configuration;
using Polly;

namespace Cauca.ApiClient.Services;

public abstract class FluentBaseService<TConfiguration> : IFluentRequestExecutor
    where TConfiguration : IConfiguration
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    protected IAsyncPolicy RetryPolicy;
    protected Func<HttpClient> Client;
    protected string ApiPrefix;
    protected TConfiguration Configuration { get; set; }
    protected virtual int MaxRetryAttemptOnTransientFailure => 3;

    protected FluentBaseService(TConfiguration configuration, IRetryPolicyBuilder policyBuilder = null, Func<HttpClient> client = null, string apiPrefix = null)
    {
        Configuration = configuration;
        Client = client ?? CreateOwnedClient;
        RetryPolicy = (policyBuilder ?? new FluentRetryPolicyBuilder()).BuildRetryPolicy(MaxRetryAttemptOnTransientFailure);
        ApiPrefix = apiPrefix;
    }

    protected FluentRequestBuilder Request(string route)
    {
        return new FluentRequestBuilder(this, route);
    }

    protected async Task<T> PostFileAsync<T>(string url, string filename, Stream stream, string contentType, CancellationToken cancellationToken = default)
    {
        stream.Position = 0;
        return await Request(url)
            .WithMultipart(multipart => multipart.AddFile(filename, stream, filename, contentType))
            .PostAsync<T>(cancellationToken);
    }

    protected async Task<T> PostFileAsync<T>(string url, string fileFullPath, string fileName, CancellationToken cancellationToken = default)
    {
        return await Request(url)
            .WithMultipart(multipart => multipart.AddFile(fileName, fileFullPath))
            .PostAsync<T>(cancellationToken);
    }

    protected async Task PostFileAsync(string url, string filename, Stream stream, string contentType, CancellationToken cancellationToken = default)
    {
        stream.Position = 0;
        await Request(url)
            .WithMultipart(multipart => multipart.AddFile(filename, stream, filename, contentType))
            .PostAsync(cancellationToken);
    }

    protected async Task PostFileAsync(string url, string fileFullPath, string fileName, CancellationToken cancellationToken = default)
    {
        await Request(url)
            .WithMultipart(multipart => multipart.AddFile(fileName, fileFullPath))
            .PostAsync(cancellationToken);
    }

    async Task IFluentRequestExecutor.SendAsync(FluentRequestDefinition request, HttpMethod method, CancellationToken cancellationToken)
    {
        await ExecuteAsync(token => SendWithoutResponseAsync(request, method, token), cancellationToken);
    }

    Task<TResult> IFluentRequestExecutor.SendAsync<TResult>(FluentRequestDefinition request, HttpMethod method, CancellationToken cancellationToken)
    {
        return ExecuteAsync(token => SendWithResponseAsync<TResult>(request, method, token), cancellationToken);
    }

    protected virtual async Task<TResult> ExecuteAsync<TResult>(Func<CancellationToken, Task<TResult>> request, CancellationToken cancellationToken)
    {
        try
        {
            return await request(cancellationToken);
        }
        catch (ApiHttpException exception)
        {
            new RestResponseValidator()
                .ThrowExceptionForStatusCode(exception.RequestUri, exception.AnswerReceived, exception.StatusCode, exception, exception.ResponseBody);
            throw;
        }
    }

    protected virtual async Task ExecuteAsync(Func<CancellationToken, Task> request, CancellationToken cancellationToken)
    {
        try
        {
            await request(cancellationToken);
        }
        catch (ApiHttpException exception)
        {
            new RestResponseValidator()
                .ThrowExceptionForStatusCode(exception.RequestUri, exception.AnswerReceived, exception.StatusCode, exception, exception.ResponseBody);
            throw;
        }
    }

    protected virtual HttpRequestMessage GenerateRequest(HttpMethod method, FluentRequestDefinition request, HttpContent content = null)
    {
        var httpRequestMessage = new HttpRequestMessage(method, RequestUriBuilder.BuildApiUri(Configuration, ApiPrefix, request.BuildRoute(), request.QueryParameters));
        foreach (var header in request.Headers)
            httpRequestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value);

        httpRequestMessage.Content = content;
        return httpRequestMessage;
    }

    private async Task SendWithoutResponseAsync(FluentRequestDefinition request, HttpMethod method, CancellationToken cancellationToken)
    {
        await RetryPolicy.ExecuteAsync(async () =>
        {
            using var httpRequest = GenerateRequest(method, request, CreateContent(method, request));
            using var response = await SendCoreAsync(httpRequest, cancellationToken);
        });
    }

    private async Task<TResult> SendWithResponseAsync<TResult>(FluentRequestDefinition request, HttpMethod method, CancellationToken cancellationToken)
    {
        return await RetryPolicy.ExecuteAsync(async () =>
        {
            using var httpRequest = GenerateRequest(method, request, CreateContent(method, request));
            using var response = await SendCoreAsync(httpRequest, cancellationToken);
            return await ReadValueAsync<TResult>(response, cancellationToken);
        });
    }

    private async Task<HttpResponseMessage> SendCoreAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var timeoutTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutTokenSource.CancelAfter(TimeSpan.FromSeconds(Configuration.RequestTimeoutInSeconds));

        try
        {
            var response = await Client().SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeoutTokenSource.Token);
            if (response.IsSuccessStatusCode)
                return response;

            throw await ApiHttpException.CreateAsync(request, response, cancellationToken);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw ApiHttpException.NoResponse(request, exception);
        }
        catch (HttpRequestException exception)
        {
            throw ApiHttpException.NoResponse(request, exception);
        }
    }

    private HttpContent CreateContent(HttpMethod method, FluentRequestDefinition request)
    {
        if (method == HttpMethod.Get && request.Body is not null)
            throw new InvalidOperationException("GET requests cannot include a body.");

        if (request.Body is not null && request.MultipartContentBuilder is not null)
            throw new InvalidOperationException("A request cannot define both a JSON body and multipart content.");

        if (request.MultipartContentBuilder is not null)
            return CreateMultipartContent(request.MultipartContentBuilder);

        if (request.Body is null)
            return null;

        return JsonContent.Create(request.Body, options: SerializerOptions);
    }

    private static MultipartFormDataContent CreateMultipartContent(Action<MultipartFormDataBuilder> configureMultipart)
    {
        var builder = new MultipartFormDataBuilder();
        configureMultipart(builder);
        return builder.Build();
    }

    private static HttpClient CreateOwnedClient()
    {
        return new HttpClient();
    }

    private static async Task<TResult> ReadValueAsync<TResult>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var type = typeof(TResult);
        if (type == typeof(string))
        {
            var value = await response.Content.ReadAsStringAsync(cancellationToken);
            return (TResult)Convert.ChangeType(value, type);
        }

        if (type == typeof(bool))
        {
            var value = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = string.Equals(value, bool.TrueString, StringComparison.OrdinalIgnoreCase);
            return (TResult)Convert.ChangeType(result, type);
        }

        if (type == typeof(int))
        {
            var value = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = int.TryParse(value, out var parsedValue) ? parsedValue : 0;
            return (TResult)Convert.ChangeType(result, type);
        }

        if (type == typeof(byte[]))
        {
            var value = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            return (TResult)(object)value;
        }

        if (type == typeof(Stream))
        {
            await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var memoryStream = new MemoryStream();
            await responseStream.CopyToAsync(memoryStream, cancellationToken);
            memoryStream.Position = 0;
            return (TResult)(object)memoryStream;
        }

        if (response.Content is null)
            return default;

        return await response.Content.ReadFromJsonAsync<TResult>(SerializerOptions, cancellationToken);
    }
}
