using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Cauca.ApiClient.Configuration;
using Cauca.ApiClient.Exceptions;
using Cauca.ApiClient.Extensions;
using Flurl;
using Flurl.Http;
using Polly;

namespace Cauca.ApiClient.Services
{
    public class RefreshTokenHandler(
        IConfiguration configuration,
        AccessInformation accessInformation,
        IAsyncPolicy policy,
        Func<HttpClient> client = null,
        string apiPrefix = null)
    {
        protected IConfiguration Configuration { get; set; } = configuration;

        private IFlurlRequest GenerateRefreshRequest()
        {
            if (client != null)
            {
                return new FlurlClient(client(), Configuration.GetAuthenticationBaseUrl())
                    .AppendRequest(apiPrefix, "Authentication")
                    .AppendPathSegment(GetPathForRefresh())
                    .WithTimeout(TimeSpan.FromSeconds(Configuration.RequestTimeoutInSeconds));
            }
            return Configuration.GetAuthenticationBaseUrl()
                .AppendPathSegment("Authentication")
                .AppendPathSegment(GetPathForRefresh())
                .WithTimeout(TimeSpan.FromSeconds(Configuration.RequestTimeoutInSeconds));
        }

        private IFlurlRequest GenerateLoginRequest()
        {
            if (client != null)
            {
                return new FlurlClient(client(), Configuration.GetAuthenticationBaseUrl())
                    .AppendRequest(apiPrefix, "Authentication")
                    .AppendPathSegment(GetPathForLogin())
                    .WithTimeout(TimeSpan.FromSeconds(Configuration.RequestTimeoutInSeconds));
            }
            return Configuration.GetAuthenticationBaseUrl()
                .AppendPathSegment("Authentication")
                .AppendPathSegment(GetPathForLogin())
                .WithTimeout(TimeSpan.FromSeconds(Configuration.RequestTimeoutInSeconds));
        }

        private string GetPathForLogin() => Configuration.UseExternalSystemLogin ? "logonforexternalsystem" : "logon";
        private string GetPathForRefresh() => Configuration.UseExternalSystemLogin ? "refreshforexternalsystem" : "refresh";

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
            var request = GenerateLoginRequest();

            try
            {
                var response = await policy.ExecuteAsync(() => request
                    .PostJsonAsync(GetLoginBody(), cancellationToken: cancellationToken)
                    .ReceiveJson<LoginResult>());
                return response;
            }
            catch (FlurlHttpException exception)
            {
                var apiException = await ToApiHttpExceptionAsync(exception);

                if (apiException.IsUnauthorized())
                    throw CreateInvalidCredentialException(apiException);

                if (apiException.NoResponse())
                    throw new NoResponseApiException(apiException);

                throw new InternalErrorApiException("An error occured in the login process", apiException);
            }
        }

        private InvalidCredentialException CreateInvalidCredentialException(Exception exception)
        {
            return Configuration.UseExternalSystemLogin
                ? new InvalidCredentialException(exception)
                : new InvalidCredentialException(Configuration.UserId, exception);
        }

        private static async Task<ApiHttpException> ToApiHttpExceptionAsync(FlurlHttpException exception)
        {
            var requestUri = exception.Call?.Request?.Url?.ToString();
            var statusCode = (HttpStatusCode?)exception.Call?.Response?.StatusCode;
            var answerReceived = exception.Call?.Response is not null;

            return new ApiHttpException(requestUri, statusCode, answerReceived, await GetResponseBodyAsync(exception, answerReceived));
        }

        private static async Task<string> GetResponseBodyAsync(FlurlHttpException exception, bool answerReceived)
        {
            if (!answerReceived)
                return null;

            try
            {
                return await exception.GetResponseStringAsync();
            }
            catch
            {
                return null;
            }
        }

        private object GetLoginBody()
        {
            if (Configuration.UseExternalSystemLogin)
                return new {ApiKey = Configuration.UserId};
            return new {Configuration.UserId, Configuration.Password};
        }

        private async Task<string> GetNewAccessToken(CancellationToken cancellationToken)
        {
            var request = GenerateRefreshRequest();

            try
            {
                var response = await policy.ExecuteAsync(() => request
                    .PostJsonAsync(GetRefreshTokenBody(), cancellationToken: cancellationToken)
                    .ReceiveJson<TokenRefreshResult>());
                return response.AccessToken;
            }
            catch (FlurlHttpException exception)
            {
                if (exception.Call.RefreshTokenIsExpired() || exception.Call.RefreshTokenIsInvalid())
                { 
                    await Login(cancellationToken);
                    return accessInformation.AccessToken;
                }
            }

            return null;
        }

        private TokenRefreshResult GetRefreshTokenBody()
        {
            return new TokenRefreshResult
            {
                AccessToken = accessInformation.AccessToken,
                RefreshToken = accessInformation.RefreshToken
            };
        }
    }
}
