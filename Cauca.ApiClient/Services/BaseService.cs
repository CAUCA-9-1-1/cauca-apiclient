using System;
using System.IO;
using System.Net;
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
        protected TConfiguration Configuration { get; set; }
        protected virtual int MaxRetryAttemptOnTransientFailure { get; } = 3;

        protected BaseService(TConfiguration configuration, IRetryPolicyBuilder? policyBuilder = null)
        {
            Configuration = configuration;
            RetryPolicy = BuildRetryPolicy(policyBuilder);
        }

        private IAsyncPolicy BuildRetryPolicy(IRetryPolicyBuilder? policyBuilder)
        {
            return (policyBuilder ?? new RetryPolicyBuilder()).BuildRetryPolicy(MaxRetryAttemptOnTransientFailure);
        }

        public async Task<TResult> PostAsync<TResult>(string url, object entity, object? headers = null)
        {
            return await ExecuteAsync(() => ExecutePostAsync<TResult>(GenerateRequest(url, headers), entity));
        }

        public async Task PostAsync(string url, object entity, object? headers = null)
        {
            await ExecuteAsync(() => ExecutePostAsync(GenerateRequest(url, headers), entity));
        }

        public async Task<TResult> PutAsync<TResult>(string url, object entity, object? headers = null)
        {
            return await ExecuteAsync(() => ExecutePutAsync<TResult>(GenerateRequest(url, headers), entity));
        }

        public async Task<TResult> DeleteAsync<TResult>(string url, object? headers = null)
        {
            return await ExecuteAsync(() => ExecuteDeleteAsync<TResult>(GenerateRequest(url, headers)));
        }

        public async Task DeleteAsync(string url, object? headers = null)
        {
            await ExecuteAsync(() => ExecuteDeleteAsync(GenerateRequest(url, headers)));
        }

        public async Task<TResult> GetAsync<TResult>(string url, object? headers = null)
        {
            return await ExecuteAsync(() => ExecuteGetAsync<TResult>(GenerateRequest(url, headers)));
        }

        public async Task GetAsync(string url, object? headers = null)
        {
            await ExecuteAsync(() => ExecuteGetAsync(GenerateRequest(url, headers)));
        }

        public async Task<byte[]> GetBytesAsync(string url, object? headers = null)
        {
            return await ExecuteAsync(() => ExecuteGetBytesAsync(GenerateRequest(url, headers)));
        }

        public async Task<Stream> GetStreamAsync(string url, object? headers = null)
        {
            return await ExecuteAsync(() => ExecuteGetStreamAsync(GenerateRequest(url, headers)));
        }

        public async Task<byte[]> PostAndReceiveBytesAsync(string url, object entity, object? headers = null)
        {
            return await ExecuteAsync(() => ExecutePostAndReceiveBytesAsync(GenerateRequest(url, headers), entity));
        }

        public async Task<Stream> PostAndReceiveStreamAsync(string url, object entity, object? headers = null)
        {
            return await ExecuteAsync(() => ExecutePostAndReceiveStreamAsync(GenerateRequest(url, headers), entity));
        }

        public async Task<string> GetStringAsync(string url, object? headers = null)
        {
            return await ExecuteAsync(() => ExecuteGetStringAsync(GenerateRequest(url, headers)));
        }

        protected async Task<T> PostFileAsync<T>(string url, string filename, Stream stream, string contentType, object? headers = null)
        {
            stream.Position = 0;            
            return await ExecuteAsync<T>(() => ExecutePostStreamAsync<T>(GenerateRequest(url, headers), mp => mp.AddFile(filename, stream, filename, contentType)));
        }

        protected async Task<T> PostFileAsync<T>(string url, string fileFullPath, string fileName, object? headers = null)
        {
            return await ExecuteAsync<T>(() => ExecutePostStreamAsync<T>(GenerateRequest(url, headers), mp => mp.AddFile(fileName, fileFullPath)));
        }

        protected async Task PostFileAsync(string url, string filename, Stream stream, string contentType, object? headers = null)
        {
            stream.Position = 0;
            await ExecuteAsync(() => ExecutePostStreamAsync(GenerateRequest(url, headers), mp => mp.AddFile(filename, stream, filename, contentType)));
        }

        protected async Task PostFileAsync(string url, string fileFullPath, string fileName, object? headers = null)
        {
            await ExecuteAsync(() => ExecutePostStreamAsync(GenerateRequest(url, headers), mp => mp.AddFile(fileName, fileFullPath)));
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

        protected virtual IFlurlRequest GenerateRequest(string url, object? headers)
        {
            var request = Configuration.ApiBaseUrl
                .AppendPathSegment(url)
                .WithHeaders(new { h1 = "foo", h2 = "bar" })
                .WithTimeout(TimeSpan.FromSeconds(Configuration.RequestTimeoutInSeconds));

            if (headers != null)
            {
                return request.WithHeaders(headers);
            }
            return request;
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