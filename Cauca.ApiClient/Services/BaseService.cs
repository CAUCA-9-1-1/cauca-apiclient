using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Cauca.ApiClient.Configuration;
using Cauca.ApiClient.Services.Interfaces;
using Flurl;
using Flurl.Http;
using Flurl.Http.Content;
using Polly;

namespace Cauca.ApiClient.Services
{
    public abstract class BaseService<TConfiguration> : IBaseService
        where TConfiguration : IConfiguration
    {
        protected IAsyncPolicy RetryPolicy;
        protected Func<HttpClient> Client;
        protected string ApiPrefix;
        protected TConfiguration Configuration { get; set; }
        protected virtual int MaxRetryAttemptOnTransientFailure => 3;

        protected BaseService(TConfiguration configuration, IRetryPolicyBuilder policyBuilder = null, Func<HttpClient> client = null, string apiPrefix = null)
        {
            Configuration = configuration;
            Client = client;
            RetryPolicy = (policyBuilder ?? new RetryPolicyBuilder()).BuildRetryPolicy(MaxRetryAttemptOnTransientFailure);
            ApiPrefix = apiPrefix;
        }

        public async Task<TResult> PostAsync<TResult>(string url, object entity)
        {
            return await ExecuteAsync(() => ExecutePostAsync<TResult>(GenerateRequest(url), entity));
        }

        public async Task PostAsync(string url, object entity)
        {
            await ExecuteAsync(() => ExecutePostAsync(GenerateRequest(url), entity));
        }

        public async Task<TResult> PutAsync<TResult>(string url, object entity)
        {
            return await ExecuteAsync(() => ExecutePutAsync<TResult>(GenerateRequest(url), entity));
        }

        public async Task<TResult> DeleteAsync<TResult>(string url)
        {
            return await ExecuteAsync(() => ExecuteDeleteAsync<TResult>(GenerateRequest(url)));
        }

        public async Task DeleteAsync(string url)
        {
            await ExecuteAsync(() => ExecuteDeleteAsync(GenerateRequest(url)));
        }

        public async Task DeleteAsync(string url, object entity)
        {
            await ExecuteAsync(() => ExecuteDeleteAsync(GenerateRequest(url), entity));
        }

        public async Task<TResult> GetAsync<TResult>(string url)
        {
            return await ExecuteAsync(() => ExecuteGetAsync<TResult>(GenerateRequest(url)));
        }

        public async Task GetAsync(string url)
        {
            await ExecuteAsync(() => ExecuteGetAsync(GenerateRequest(url)));
        }

        public async Task<byte[]> GetBytesAsync(string url)
        {
            return await ExecuteAsync(() => ExecuteGetBytesAsync(GenerateRequest(url)));
        }

        public async Task<Stream> GetStreamAsync(string url)
        {
            return await ExecuteAsync(() => ExecuteGetStreamAsync(GenerateRequest(url)));
        }

        public async Task<byte[]> PostAndReceiveBytesAsync(string url, object entity)
        {
            return await ExecuteAsync(() => ExecutePostAndReceiveBytesAsync(GenerateRequest(url), entity));
        }

        public async Task<Stream> PostAndReceiveStreamAsync(string url, object entity)
        {
            return await ExecuteAsync(() => ExecutePostAndReceiveStreamAsync(GenerateRequest(url), entity));
        }

        public async Task<string> GetStringAsync(string url)
        {
            return await ExecuteAsync(() => ExecuteGetStringAsync(GenerateRequest(url)));
        }

        protected async Task<T> PostFileAsync<T>(string url, string filename, Stream stream, string contentType)
        {
            stream.Position = 0;            
            return await ExecuteAsync(() => ExecutePostStreamAsync<T>(GenerateRequest(url), mp => mp.AddFile(filename, stream, filename, contentType)));
        }

        protected async Task<T> PostFileAsync<T>(string url, string fileFullPath, string fileName)
        {
            return await ExecuteAsync(() => ExecutePostStreamAsync<T>(GenerateRequest(url), mp => mp.AddFile(fileName, fileFullPath)));
        }

        protected async Task PostFileAsync(string url, string filename, Stream stream, string contentType)
        {
            stream.Position = 0;
            await ExecuteAsync(() => ExecutePostStreamAsync(GenerateRequest(url), mp => mp.AddFile(filename, stream, filename, contentType)));
        }

        protected async Task PostFileAsync(string url, string fileFullPath, string fileName)
        {
            await ExecuteAsync(() => ExecutePostStreamAsync(GenerateRequest(url), mp => mp.AddFile(fileName, fileFullPath)));
        }

        protected virtual async Task<TResult> ExecuteAsync<TResult>(Func<Task<TResult>> request)
        {
            try
            {
                return await request();
            }
            catch (FlurlHttpException exception)
            {
                new RestResponseValidator()
                    .ThrowExceptionForStatusCode(exception.Call.Request.Url, exception.Call.Succeeded, (HttpStatusCode?)exception.Call.Response?.StatusCode, exception);
                throw;
            }
        }

        protected virtual async Task ExecuteAsync(Func<Task> request)
        {
            try
            {
                await request();
            }
            catch (FlurlHttpException exception)
            {
                new RestResponseValidator()
                    .ThrowExceptionForStatusCode(exception.Call.Request.Url, exception.Call.Succeeded, (HttpStatusCode?)exception.Call.Response?.StatusCode, exception);
                throw;
            }
        }

        protected virtual IFlurlRequest GenerateRequest(string url)
        {
            if (Client != null)
            {
                return new FlurlClient(Client(), Configuration.GetBaseUrl())
                    .AppendRequest(ApiPrefix, url)
                    .WithTimeout(TimeSpan.FromSeconds(Configuration.RequestTimeoutInSeconds));
            }
            return Configuration.GetBaseUrl()
                .AppendPathSegment(url)
                .WithTimeout(TimeSpan.FromSeconds(Configuration.RequestTimeoutInSeconds));
        }

        protected async Task ExecutePostAsync(IFlurlRequest request, object entity)
        {
            await RetryPolicy.ExecuteAsync(() => request.PostJsonAsync(entity));
        }

        protected async Task<TResult> ExecutePostAsync<TResult>(IFlurlRequest request, object entity)
        {
            var type = typeof(TResult);
            if (type == typeof(string))
            {
                var response = await RetryPolicy.ExecuteAsync(() => request
                    .PostJsonAsync(entity)
                    .ReceiveString());
                return (TResult)Convert.ChangeType(response, typeof(TResult));
            }
            else if (type == typeof(bool))
            {
                var response = (await RetryPolicy.ExecuteAsync(() => request
                    .PostJsonAsync(entity)
                    .ReceiveString())).ToUpper() == "TRUE";
                return (TResult)Convert.ChangeType(response, typeof(TResult));
            }
            else if (type == typeof(int))
            {
                var response = await RetryPolicy.ExecuteAsync(() => request
                    .PostJsonAsync(entity)
                    .ReceiveString());
                if (int.TryParse(response, out int result))
                {
                    return (TResult)Convert.ChangeType(result, typeof(TResult));
                }

                return (TResult)Convert.ChangeType(0, typeof(TResult));
            }
            else
            {
                return await RetryPolicy.ExecuteAsync(() => request
                    .PostJsonAsync(entity)
                    .ReceiveJson<TResult>());
            }
        }

        protected async Task<TResult> ExecuteGetAsync<TResult>(IFlurlRequest request)
        {
            var type = typeof(TResult);
            if (type == typeof(string))
            {
                var response = await RetryPolicy.ExecuteAsync(() => request.GetStringAsync());
                return (TResult) Convert.ChangeType(response, typeof(TResult));
            }
            else if (type == typeof(bool))
            {
                var response = (await RetryPolicy.ExecuteAsync(() => request.GetStringAsync())).ToUpper() == "TRUE";
                return (TResult)Convert.ChangeType(response, typeof(TResult));
            }
            else if (type == typeof(int))
            {
                var response = await RetryPolicy.ExecuteAsync(() => request.GetStringAsync());
                if (int.TryParse(response, out int result))
                {
                    return (TResult)Convert.ChangeType(result, typeof(TResult));
                }

                return (TResult)Convert.ChangeType(0, typeof(TResult));
            }
            else
            {
                return await RetryPolicy.ExecuteAsync(() => request
                    .GetJsonAsync<TResult>());
            }
        }

        protected async Task ExecuteGetAsync(IFlurlRequest request)
        {
            await RetryPolicy.ExecuteAsync(() => request
                .GetAsync());
        }

        protected async Task<Stream> ExecuteGetStreamAsync(IFlurlRequest request)
        {
            return await RetryPolicy.ExecuteAsync(() => request.GetStreamAsync());
        }

        protected async Task<string> ExecuteGetStringAsync(IFlurlRequest request)
        {
            return await RetryPolicy.ExecuteAsync(() => request.GetStringAsync());
        }

        protected async Task<byte[]> ExecuteGetBytesAsync(IFlurlRequest request)
        {
            return await RetryPolicy.ExecuteAsync(() => request.GetBytesAsync());
        }

        protected async Task<TResult> ExecutePutAsync<TResult>(IFlurlRequest request, object entity)
        {
            return await RetryPolicy.ExecuteAsync(() => request
                .PutJsonAsync(entity)
                .ReceiveJson<TResult>());
        }

        protected async Task<TResult> ExecuteDeleteAsync<TResult>(IFlurlRequest request)
        {
            return await RetryPolicy.ExecuteAsync(() => request
                .DeleteAsync()
                .ReceiveJson<TResult>());
        }


        protected async Task ExecuteDeleteAsync(IFlurlRequest request)
        {
            await RetryPolicy.ExecuteAsync(() => request
                .DeleteAsync());
        }

        protected async Task ExecuteDeleteAsync(IFlurlRequest request, object entity)
        {
            await RetryPolicy.ExecuteAsync(() => request
                .SendJsonAsync(HttpMethod.Delete, entity));
        }

        protected async Task<TResult> ExecutePostStreamAsync<TResult>(IFlurlRequest request, Action<CapturedMultipartContent> action)
        {
            return await request
                .PostMultipartAsync(action)
                .ReceiveJson<TResult>();
        }

        protected async Task ExecutePostStreamAsync(IFlurlRequest request, Action<CapturedMultipartContent> action)
        {
            await request
                .PostMultipartAsync(action);
        }

        protected async Task<byte[]> ExecutePostAndReceiveBytesAsync(IFlurlRequest request, object entity)
        {
            return await RetryPolicy.ExecuteAsync(() => request.PostJsonAsync(entity).ReceiveBytes ());
        }

        protected async Task<Stream> ExecutePostAndReceiveStreamAsync(IFlurlRequest request, object entity)
        {
            return await RetryPolicy.ExecuteAsync(() => request.PostJsonAsync(entity).ReceiveStream());
        }

    }
}