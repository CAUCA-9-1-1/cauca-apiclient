using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Cauca.ApiClient.Configuration;
using Cauca.ApiClient.Extensions;
using Flurl.Http;

namespace Cauca.ApiClient.Services
{
    public abstract class BaseSecureService<TConfiguration> 
        : BaseService<TConfiguration> 
        where TConfiguration : IConfiguration
    {
        public readonly AccessInformation AccessInformation = new();

        protected BaseSecureService(TConfiguration configuration, IRetryPolicyBuilder policyBuilder = null)
            : base(configuration, policyBuilder)
        {
        }

        protected BaseSecureService(TConfiguration configuration, Func<HttpClient> client, string apiPrefix) 
            : base(configuration, null, client, apiPrefix)
        {
        }

        protected override IFlurlRequest GenerateRequest(string url)
        {
            return base.GenerateRequest(url)
                .WithHeader("Authorization", GetAuthorizationHeaderValue());
        }

        protected string GetAuthorizationHeaderValue()
        {
            return $"{AccessInformation.AuthorizationType} {AccessInformation.AccessToken}";
        }

        protected override async Task<TResult> ExecuteAsync<TResult>(Func<Task<TResult>> request)
        {
            await LoginWhenLoggedOut();
            try
            {
                return await request();
            }
            catch (FlurlHttpException exception)
            {
                if (exception.Call.AccessTokenIsExpired())
                {
                    return await RefreshTokenThenRetry(request);
                }

                new RestResponseValidator()
                    .ThrowExceptionForStatusCode(exception.Call.Request.Url, exception.Call.Succeeded,
                        (HttpStatusCode?)exception.Call.Response?.StatusCode, exception);
                throw;
            }
        }

        protected override async Task ExecuteAsync(Func<Task> request)
        {
            await LoginWhenLoggedOut();
            try
            {
                await request();
            }
            catch (FlurlHttpException exception)
            {
                if (exception.Call.AccessTokenIsExpired())
                {
                    await RefreshTokenThenRetry(request);
                }

                new RestResponseValidator()
                    .ThrowExceptionForStatusCode(exception.Call.Request.Url, exception.Call.Succeeded,
                        (HttpStatusCode?)exception.Call.Response?.StatusCode, exception);
                throw;
            }
        }

        protected async Task LoginWhenLoggedOut()
        {
            if (string.IsNullOrWhiteSpace(AccessInformation.AccessToken))
                await GetRefreshTokenHandler()
                    .Login();
        }

        private async Task<TResult> RefreshTokenThenRetry<TResult>(Func<Task<TResult>> action)
        {
            await GetRefreshTokenHandler().RefreshToken();
            return await action();
        }
        private async Task RefreshTokenThenRetry(Func<Task> action)
        {
            await GetRefreshTokenHandler().RefreshToken();
            await action();
        }

        private RefreshTokenHandler GetRefreshTokenHandler()
        {
            return new RefreshTokenHandler(Configuration, AccessInformation, RetryPolicy, Client, ApiPrefix);
        }
    }

    public class AccessInformation
    {
        public string AuthorizationType { get; set; } = "";
        public string AccessToken { get; set; } = "";
        public string RefreshToken { get; set; } = "";

    }
}