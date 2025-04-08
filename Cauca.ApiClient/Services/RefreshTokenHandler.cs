using System;
using System.Net.Http;
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

        public async Task RefreshToken()
        {
            var token = await GetNewAccessToken();
            accessInformation.AccessToken = token;
        }

        public async Task Login()
        {
            var login = await GetInitialAccessToken();
            accessInformation.AuthorizationType = login.AuthorizationType;
            accessInformation.AccessToken = login.AccessToken;
            accessInformation.RefreshToken = login.RefreshToken;
        }

        private async Task<LoginResult> GetInitialAccessToken()
        {
            var request = GenerateLoginRequest();

            try
            {
                var response = await policy.ExecuteAsync(() => request
                    .PostJsonAsync(GetLoginBody())
                    .ReceiveJson<LoginResult>());
                return response;
            }
            catch (FlurlHttpException exception)
            {
                if (exception.Call.IsUnauthorized())
                    throw new InvalidCredentialException(Configuration.UserId, exception);

                if (exception.Call.NoResponse())
                    throw new NoResponseApiException(exception);

                throw new InternalErrorApiException("An error occured in the login process", exception);
            }
        }

        private object GetLoginBody()
        {
            if (Configuration.UseExternalSystemLogin)
                return new {ApiKey = Configuration.UserId};
            return new {Configuration.UserId, Configuration.Password};
        }

        private async Task<string> GetNewAccessToken()
        {
            var request = GenerateRefreshRequest();

            try
            {
                var response = await policy.ExecuteAsync(() => request
                    .PostJsonAsync(GetRefreshTokenBody())
                    .ReceiveJson<TokenRefreshResult>());
                return response.AccessToken;
            }
            catch (FlurlHttpException exception)
            {
                if (exception.Call.RefreshTokenIsExpired() || exception.Call.RefreshTokenIsInvalid())
                { 
                    await Login();
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