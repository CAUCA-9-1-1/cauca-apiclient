using System;
using System.Net;
using System.Net.Http;
using System.Threading;
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

        protected override async Task<TResult> ExecuteAsync<TResult>(Func<CancellationToken, Task<TResult>> request, CancellationToken cancellationToken)
        {
            await LoginWhenLoggedOut(cancellationToken);
            try
            {
                return await request(cancellationToken);
            }
            catch (FlurlHttpException exception)
            {
                if (exception.Call.AccessTokenIsExpired())
                {
                    return await RefreshTokenThenRetry(request, cancellationToken);
                }

                new RestResponseValidator()
                    .ThrowExceptionForStatusCode(exception.Call.Request.Url, exception.Call.Succeeded,
                        (HttpStatusCode?)exception.Call.Response?.StatusCode, exception, await GetBodyAsync(exception, cancellationToken));
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
            catch (FlurlHttpException exception)
            {
                if (exception.Call.AccessTokenIsExpired())
                {
                    await RefreshTokenThenRetry(request, cancellationToken);
                }

                new RestResponseValidator()
                    .ThrowExceptionForStatusCode(exception.Call.Request.Url, exception.Call.Succeeded,
                        (HttpStatusCode?)exception.Call.Response?.StatusCode, exception, await GetBodyAsync(exception, cancellationToken));
                throw;
            }
        }

        protected async Task LoginWhenLoggedOut(CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(AccessInformation.AccessToken))
                await GetRefreshTokenHandler()
                    .Login(cancellationToken);
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