using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Cauca.ApiClient.Configuration;
using Cauca.ApiClient.Exceptions;
using Polly;

namespace Cauca.ApiClient.Services;

internal sealed class FluentRefreshTokenHandler(
    IConfiguration configuration,
    AccessInformation accessInformation,
    IAsyncPolicy policy,
    Func<HttpClient> client = null,
    string apiPrefix = null)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    protected IConfiguration Configuration { get; set; } = configuration;

    public async Task RefreshToken(CancellationToken cancellationToken = default)
    {
        var token = await GetNewAccessToken(cancellationToken);
        accessInformation.AccessToken = token;
    }

    public async Task Login(CancellationToken cancellationToken = default)
    {
        var login = await GetInitialAccessToken(cancellationToken);
        accessInformation.AuthorizationType = login.AuthorizationType;
        accessInformation.AccessToken = login.AccessToken;
        accessInformation.RefreshToken = login.RefreshToken;
    }

    private async Task<LoginResult> GetInitialAccessToken(CancellationToken cancellationToken)
    {
        try
        {
            return await ExecuteAsync(
                GenerateLoginRequest,
                cancellationToken,
                static (response, ct) => response.Content.ReadFromJsonAsync<LoginResult>(SerializerOptions, ct));
        }
        catch (ApiHttpException exception)
        {
            if (exception.IsUnauthorized())
                throw new InvalidCredentialException(Configuration.UserId, exception);

            if (exception.NoResponse())
                throw new NoResponseApiException(exception);

            throw new InternalErrorApiException("An error occured in the login process", exception);
        }
    }

    private async Task<string> GetNewAccessToken(CancellationToken cancellationToken)
    {
        try
        {
            var response = await ExecuteAsync(
                GenerateRefreshRequest,
                cancellationToken,
                static (httpResponse, ct) => httpResponse.Content.ReadFromJsonAsync<TokenRefreshResult>(SerializerOptions, ct));
            return response.AccessToken;
        }
        catch (ApiHttpException exception)
        {
            if (exception.RefreshTokenIsExpired() || exception.RefreshTokenIsInvalid())
            {
                await Login(cancellationToken);
                return accessInformation.AccessToken;
            }
        }

        return null;
    }

    private HttpRequestMessage GenerateRefreshRequest()
    {
        return new HttpRequestMessage(
            HttpMethod.Post,
            RequestUriBuilder.BuildAuthenticationUri(Configuration, apiPrefix, $"Authentication/{GetPathForRefresh()}"))
        {
            Content = JsonContent.Create(new TokenRefreshResult
            {
                AccessToken = accessInformation.AccessToken,
                RefreshToken = accessInformation.RefreshToken
            }, options: SerializerOptions)
        };
    }

    private HttpRequestMessage GenerateLoginRequest()
    {
        return new HttpRequestMessage(
            HttpMethod.Post,
            RequestUriBuilder.BuildAuthenticationUri(Configuration, apiPrefix, $"Authentication/{GetPathForLogin()}"))
        {
            Content = JsonContent.Create(GetLoginBody(), options: SerializerOptions)
        };
    }

    private object GetLoginBody()
    {
        if (Configuration.UseExternalSystemLogin)
            return new { ApiKey = Configuration.UserId };

        return new { Configuration.UserId, Configuration.Password };
    }

    private string GetPathForLogin() => Configuration.UseExternalSystemLogin ? "logonforexternalsystem" : "logon";
    private string GetPathForRefresh() => Configuration.UseExternalSystemLogin ? "refreshforexternalsystem" : "refresh";

    private async Task<TResult> ExecuteAsync<TResult>(
        Func<HttpRequestMessage> requestFactory,
        CancellationToken cancellationToken,
        Func<HttpResponseMessage, CancellationToken, Task<TResult>> responseReader)
    {
        return await policy.ExecuteAsync(async () =>
        {
            using var request = requestFactory();
            using var timeoutTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutTokenSource.CancelAfter(TimeSpan.FromSeconds(Configuration.RequestTimeoutInSeconds));

            try
            {
                var httpClient = client?.Invoke() ?? new HttpClient();
                var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeoutTokenSource.Token);
                if (!response.IsSuccessStatusCode)
                {
                    using (response)
                        throw await ApiHttpException.CreateAsync(request, response, cancellationToken);
                }

                using (response)
                    return await responseReader(response, cancellationToken);
            }
            catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                throw ApiHttpException.NoResponse(request, exception);
            }
            catch (HttpRequestException exception)
            {
                throw ApiHttpException.NoResponse(request, exception);
            }
        });
    }
}
