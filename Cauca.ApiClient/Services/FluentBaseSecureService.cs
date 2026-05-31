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

    protected override HttpRequestMessage GenerateRequest(HttpMethod method, FluentRequestDefinition request, HttpContent content = null)
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
        try
        {
            return await request(cancellationToken);
        }
        catch (ApiHttpException exception)
        {
            if (exception.AccessTokenIsExpired())
                return await RefreshTokenThenRetry(request, cancellationToken);

            new RestResponseValidator()
                .ThrowExceptionForStatusCode(exception.RequestUri, exception.AnswerReceived, exception.StatusCode, exception, exception.ResponseBody);
            throw;
        }
    }

    protected override async Task ExecuteAsync(Func<CancellationToken, Task> request, CancellationToken cancellationToken)
    {
        await LoginWhenLoggedOut(cancellationToken);
        try
        {
            await request(cancellationToken);
        }
        catch (ApiHttpException exception)
        {
            if (exception.AccessTokenIsExpired())
            {
                await RefreshTokenThenRetry(request, cancellationToken);
                return;
            }

            new RestResponseValidator()
                .ThrowExceptionForStatusCode(exception.RequestUri, exception.AnswerReceived, exception.StatusCode, exception, exception.ResponseBody);
            throw;
        }
    }

    protected async Task LoginWhenLoggedOut(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(AccessInformation.AccessToken))
            await GetRefreshTokenHandler().Login(cancellationToken);
    }

    private async Task<TResult> RefreshTokenThenRetry<TResult>(Func<CancellationToken, Task<TResult>> action, CancellationToken cancellationToken)
    {
        await GetRefreshTokenHandler().RefreshToken(cancellationToken);
        return await action(cancellationToken);
    }

    private async Task RefreshTokenThenRetry(Func<CancellationToken, Task> action, CancellationToken cancellationToken)
    {
        await GetRefreshTokenHandler().RefreshToken(cancellationToken);
        await action(cancellationToken);
    }

    private FluentRefreshTokenHandler GetRefreshTokenHandler()
    {
        return new FluentRefreshTokenHandler(Configuration, AccessInformation, RetryPolicy, Client, ApiPrefix);
    }
}
