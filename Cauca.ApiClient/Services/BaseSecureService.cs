using System;
using System.Net;
using System.Threading.Tasks;
using Cauca.ApiClient.Configuration;
using Cauca.ApiClient.Extensions;
using Flurl.Http;

namespace Cauca.ApiClient.Services
{
    public abstract class BaseSecureService<TConfiguration> 
        : BaseService<TConfiguration> 
        where TConfiguration : BaseApiClientConfiguration
    {
        public readonly AccessInformation AccessInformation = new AccessInformation();

        protected BaseSecureService(TConfiguration baseApiClientConfiguration, IRetryPolicyBuilder policyBuilder = null) 
            : base(baseApiClientConfiguration, policyBuilder)
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
                await new RefreshTokenHandler(Configuration, AccessInformation, RetryPolicy)
                    .Login();
        }

        private async Task<TResult> RefreshTokenThenRetry<TResult>(Func<Task<TResult>> action)
        {
            await new RefreshTokenHandler(Configuration, AccessInformation, RetryPolicy)
                .RefreshToken();
            return await action();
        }
        private async Task RefreshTokenThenRetry(Func<Task> action)
        {
            await new RefreshTokenHandler(Configuration, AccessInformation, RetryPolicy)
                .RefreshToken();
            await action();
        }
    }

    public class AccessInformation
    {
        public string AuthorizationType { get; set; } = "";
        public string AccessToken { get; set; } = "";
        public string RefreshToken { get; set; } = "";

    }
}