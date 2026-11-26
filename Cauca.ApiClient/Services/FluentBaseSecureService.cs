using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Cauca.ApiClient.Configuration;

namespace Cauca.ApiClient.Services;

public abstract class FluentBaseSecureService<TConfiguration> : FluentBaseService<TConfiguration>
    where TConfiguration : IConfiguration
{
    public readonly AccessInformation AccessInformation = new();

    protected FluentBaseSecureService(TConfiguration configuration, IRetryPolicyBuilder policyBuilder = null)
        : base(configuration, policyBuilder)
    {
    }

    protected FluentBaseSecureService(TConfiguration configuration, Func<HttpClient> client, string apiPrefix)
        : base(configuration, null, client, apiPrefix)
    {
    }

    private protected override HttpRequestMessage GenerateRequest(HttpMethod method, FluentRequestDefinition request, HttpContent content = null)
    {
        var httpRequestMessage = base.GenerateRequest(method, request, content);
        httpRequestMessage.Headers.TryAddWithoutValidation("Authorization", GetAuthorizationHeaderValue());
        return httpRequestMessage;
    }

    protected string GetAuthorizationHeaderValue()
    {
        return $"{AccessInformation.AuthorizationType} {AccessInformation.AccessToken}";
    }

    protected override async Task<TResult> ExecuteAsync<TResult>(Func<CancellationToken, Task<TResult>> request, CancellationToken cancellationToken)
    {
        await LoginWhenLoggedOut(cancellationToken);
        return await base.ExecuteAsync(token => RetryOnceAfterTokenRefresh(request, token), cancellationToken);
    }

    protected override async Task ExecuteAsync(Func<CancellationToken, Task> request, CancellationToken cancellationToken)
    {
        await LoginWhenLoggedOut(cancellationToken);
        await base.ExecuteAsync(token => RetryOnceAfterTokenRefresh(request, token), cancellationToken);
    }

    protected async Task LoginWhenLoggedOut(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(AccessInformation.AccessToken))
            await GetRefreshTokenHandler().Login(cancellationToken);
    }

    private async Task<TResult> RetryOnceAfterTokenRefresh<TResult>(Func<CancellationToken, Task<TResult>> request, CancellationToken cancellationToken)
    {
        try
        {
            return await request(cancellationToken);
        }
        catch (ApiHttpException exception) when (exception.AccessTokenIsExpired())
        {
            await GetRefreshTokenHandler().RefreshToken(cancellationToken);
            return await request(cancellationToken);
        }
    }

    private async Task RetryOnceAfterTokenRefresh(Func<CancellationToken, Task> request, CancellationToken cancellationToken)
    {
        try
        {
            await request(cancellationToken);
        }
        catch (ApiHttpException exception) when (exception.AccessTokenIsExpired())
        {
            await GetRefreshTokenHandler().RefreshToken(cancellationToken);
            await request(cancellationToken);
        }
    }

    private FluentRefreshTokenHandler GetRefreshTokenHandler()
    {
        return new FluentRefreshTokenHandler(Configuration, AccessInformation, RetryPolicy, Client, ApiPrefix);
    }
}
