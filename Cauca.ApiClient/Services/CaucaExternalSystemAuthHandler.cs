using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Cauca.ApiClient.Configuration;
using Cauca.ApiClient.Extensions;
using Polly;

namespace Cauca.ApiClient.Services;

internal sealed class CaucaExternalSystemAuthHandler : DelegatingHandler
{
    private readonly IConfiguration _configuration;
    private readonly string _apiPrefix;
    private readonly Func<HttpClient> _authClientFactory;
    private readonly IAsyncPolicy _policy;
    private readonly AccessInformation _accessInformation = new();
    private readonly SemaphoreSlim _loginGate = new(1, 1);

    public CaucaExternalSystemAuthHandler(IConfiguration configuration, string apiPrefix, Func<HttpClient> authClientFactory, IAsyncPolicy policy)
    {
        _configuration = configuration;
        _apiPrefix = apiPrefix;
        _authClientFactory = authClientFactory;
        _policy = policy;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await EnsureLoggedInAsync(cancellationToken);
        if (request.Content is not null)
            await request.Content.LoadIntoBufferAsync();

        SetAuthorizationHeader(request);
        var response = await base.SendAsync(request, cancellationToken);
        if (!response.IsUnauthorized())
            return response;

        if (response.AccessTokenIsExpired())
        {
            response.Dispose();
            await CreateRefreshTokenHandler().RefreshToken(cancellationToken);
            return await ResendAsync(request, cancellationToken);
        }

        if (response.RefreshTokenIsExpired() || response.RefreshTokenIsInvalid())
        {
            response.Dispose();
            await CreateRefreshTokenHandler().Login(cancellationToken);
            return await ResendAsync(request, cancellationToken);
        }

        return response;
    }

    private async Task<HttpResponseMessage> ResendAsync(HttpRequestMessage originalRequest, CancellationToken cancellationToken)
    {
        using var retry = await CloneAsync(originalRequest);
        SetAuthorizationHeader(retry);
        return await base.SendAsync(retry, cancellationToken);
    }

    private static async Task<HttpRequestMessage> CloneAsync(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version
        };

        if (request.Content is not null)
        {
            var buffer = await request.Content.ReadAsByteArrayAsync();
            clone.Content = new ByteArrayContent(buffer);
            foreach (var header in request.Content.Headers)
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        foreach (var header in request.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

        return clone;
    }

    private async Task EnsureLoggedInAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_accessInformation.AccessToken))
            return;

        await _loginGate.WaitAsync(cancellationToken);
        try
        {
            if (string.IsNullOrWhiteSpace(_accessInformation.AccessToken))
                await CreateRefreshTokenHandler().Login(cancellationToken);
        }
        finally
        {
            _loginGate.Release();
        }
    }

    private void SetAuthorizationHeader(HttpRequestMessage request)
    {
        request.Headers.Remove("Authorization");
        request.Headers.TryAddWithoutValidation("Authorization", $"{_accessInformation.AuthorizationType} {_accessInformation.AccessToken}");
    }

    private FluentRefreshTokenHandler CreateRefreshTokenHandler()
    {
        return new FluentRefreshTokenHandler(_configuration, _accessInformation, _policy, _authClientFactory, _apiPrefix);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _loginGate.Dispose();
        base.Dispose(disposing);
    }
}
