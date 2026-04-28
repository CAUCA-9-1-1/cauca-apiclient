using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
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

        public async Task<TResult> PostAsync<TResult>(string url, object entity, CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(ct => ExecutePostAsync<TResult>(GenerateRequest(url), entity, ct), cancellationToken);
        }

        public async Task PostAsync(string url, object entity, CancellationToken cancellationToken = default)
        {
            await ExecuteAsync(ct => ExecutePostAsync(GenerateRequest(url), entity, ct), cancellationToken);
        }

        public async Task<TResult> PutAsync<TResult>(string url, object entity, CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(ct => ExecutePutAsync<TResult>(GenerateRequest(url), entity, ct), cancellationToken);
        }

        public async Task<TResult> DeleteAsync<TResult>(string url, CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(ct => ExecuteDeleteAsync<TResult>(GenerateRequest(url), ct), cancellationToken);
        }

        public async Task DeleteAsync(string url, CancellationToken cancellationToken = default)
        {
            await ExecuteAsync(ct => ExecuteDeleteAsync(GenerateRequest(url), ct), cancellationToken);
        }

        public async Task DeleteAsync(string url, object entity, CancellationToken cancellationToken = default)
        {
            await ExecuteAsync(ct => ExecuteDeleteAsync(GenerateRequest(url), entity, ct), cancellationToken);
        }

        public async Task<TResult> GetAsync<TResult>(string url, CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(ct => ExecuteGetAsync<TResult>(GenerateRequest(url), ct), cancellationToken);
        }

        public async Task GetAsync(string url, CancellationToken cancellationToken = default)
        {
            await ExecuteAsync(ct => ExecuteGetAsync(GenerateRequest(url), ct), cancellationToken);
        }

        public async Task<byte[]> GetBytesAsync(string url, CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(ct => ExecuteGetBytesAsync(GenerateRequest(url), ct), cancellationToken);
        }

        public async Task<Stream> GetStreamAsync(string url, CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(ct => ExecuteGetStreamAsync(GenerateRequest(url), ct), cancellationToken);
        }

        public async Task<byte[]> PostAndReceiveBytesAsync(string url, object entity, CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(ct => ExecutePostAndReceiveBytesAsync(GenerateRequest(url), entity, ct), cancellationToken);
        }

        public async Task<Stream> PostAndReceiveStreamAsync(string url, object entity, CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(ct => ExecutePostAndReceiveStreamAsync(GenerateRequest(url), entity, ct), cancellationToken);
        }

        public async Task<string> GetStringAsync(string url, CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(ct => ExecuteGetStringAsync(GenerateRequest(url), ct), cancellationToken);
        }

        protected async Task<T> PostFileAsync<T>(string url, string filename, Stream stream, string contentType, CancellationToken cancellationToken = default)
        {
            stream.Position = 0;
            return await ExecuteAsync(ct => ExecutePostStreamAsync<T>(GenerateRequest(url), mp => mp.AddFile(filename, stream, filename, contentType), ct), cancellationToken);
        }

        protected async Task<T> PostFileAsync<T>(string url, string fileFullPath, string fileName, CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(ct => ExecutePostStreamAsync<T>(GenerateRequest(url), mp => mp.AddFile(fileName, fileFullPath), ct), cancellationToken);
        }

        protected async Task PostFileAsync(string url, string filename, Stream stream, string contentType, CancellationToken cancellationToken = default)
        {
            stream.Position = 0;
            await ExecuteAsync(ct => ExecutePostStreamAsync(GenerateRequest(url), mp => mp.AddFile(filename, stream, filename, contentType), ct), cancellationToken);
        }

        protected async Task PostFileAsync(string url, string fileFullPath, string fileName, CancellationToken cancellationToken = default)
        {
            await ExecuteAsync(ct => ExecutePostStreamAsync(GenerateRequest(url), mp => mp.AddFile(fileName, fileFullPath), ct), cancellationToken);
        }

        protected virtual async Task<TResult> ExecuteAsync<TResult>(Func<CancellationToken, Task<TResult>> request, CancellationToken cancellationToken)
        {
            try
            {
                return await request(cancellationToken);
            }
            catch (FlurlHttpException exception)
            {
                var body = await GetBodyAsync(exception, cancellationToken);
                new RestResponseValidator()
                    .ThrowExceptionForStatusCode(exception.Call!.Request.Url, exception.Call.Succeeded, (HttpStatusCode?)exception.Call.Response?.StatusCode, exception, body);
                throw;
            }
        }

        protected static async Task<string> GetBodyAsync(FlurlHttpException exception, CancellationToken cancellationToken)
        {
            try
            {
                if (exception.Call?.Response is FlurlResponse { ResponseMessage.Content: CapturedStringContent content })
                    return content.Content;
                if (exception.Call?.Response is FlurlResponse response && response.ResponseMessage.Content is HttpContent httpContent)
                    return await httpContent.ReadAsStringAsync(cancellationToken);
                if (exception.Call?.Response is FlurlResponse)
                    return await exception.Call.Response.GetStringAsync();
            }
            catch
            {
                return null;
            }
            return null;
        }

        protected virtual async Task ExecuteAsync(Func<CancellationToken, Task> request, CancellationToken cancellationToken)
        {
            try
            {
                await request(cancellationToken);
            }
            catch (FlurlHttpException exception)
            {
                new RestResponseValidator()
                    .ThrowExceptionForStatusCode(exception.Call.Request.Url, exception.Call.Succeeded, (HttpStatusCode?)exception.Call.Response?.StatusCode, exception, await GetBodyAsync(exception, cancellationToken));
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

        protected async Task ExecutePostAsync(IFlurlRequest request, object entity, CancellationToken cancellationToken)
        {
            await RetryPolicy.ExecuteAsync(() => request.PostJsonAsync(entity, cancellationToken: cancellationToken));
        }

        protected async Task<TResult> ExecutePostAsync<TResult>(IFlurlRequest request, object entity, CancellationToken cancellationToken)
        {
            var type = typeof(TResult);
            if (type == typeof(string))
            {
                var response = await RetryPolicy.ExecuteAsync(() => request
                    .PostJsonAsync(entity, cancellationToken: cancellationToken)
                    .ReceiveString());
                return (TResult)Convert.ChangeType(response, typeof(TResult));
            }
            else if (type == typeof(bool))
            {
                var response = (await RetryPolicy.ExecuteAsync(() => request
                    .PostJsonAsync(entity, cancellationToken: cancellationToken)
                    .ReceiveString())).ToUpper() == "TRUE";
                return (TResult)Convert.ChangeType(response, typeof(TResult));
            }
            else if (type == typeof(int))
            {
                var response = await RetryPolicy.ExecuteAsync(() => request
                    .PostJsonAsync(entity, cancellationToken: cancellationToken)
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
                    .PostJsonAsync(entity, cancellationToken: cancellationToken)
                    .ReceiveJson<TResult>());
            }
        }

        protected async Task<TResult> ExecuteGetAsync<TResult>(IFlurlRequest request, CancellationToken cancellationToken)
        {
            var type = typeof(TResult);
            if (type == typeof(string))
            {
                var response = await RetryPolicy.ExecuteAsync(() => request.GetStringAsync(cancellationToken: cancellationToken));
                return (TResult) Convert.ChangeType(response, typeof(TResult));
            }
            else if (type == typeof(bool))
            {
                var response = (await RetryPolicy.ExecuteAsync(() => request.GetStringAsync(cancellationToken: cancellationToken))).ToUpper() == "TRUE";
                return (TResult)Convert.ChangeType(response, typeof(TResult));
            }
            else if (type == typeof(int))
            {
                var response = await RetryPolicy.ExecuteAsync(() => request.GetStringAsync(cancellationToken: cancellationToken));
                if (int.TryParse(response, out int result))
                {
                    return (TResult)Convert.ChangeType(result, typeof(TResult));
                }

                return (TResult)Convert.ChangeType(0, typeof(TResult));
            }
            else
            {
                return await RetryPolicy.ExecuteAsync(() => request
                    .GetJsonAsync<TResult>(cancellationToken: cancellationToken));
            }
        }

        protected async Task ExecuteGetAsync(IFlurlRequest request, CancellationToken cancellationToken)
        {
            await RetryPolicy.ExecuteAsync(() => request
                .GetAsync(cancellationToken: cancellationToken));
        }

        protected async Task<Stream> ExecuteGetStreamAsync(IFlurlRequest request, CancellationToken cancellationToken)
        {
            return await RetryPolicy.ExecuteAsync(() => request.GetStreamAsync(cancellationToken: cancellationToken));
        }

        protected async Task<string> ExecuteGetStringAsync(IFlurlRequest request, CancellationToken cancellationToken)
        {
            return await RetryPolicy.ExecuteAsync(() => request.GetStringAsync(cancellationToken: cancellationToken));
        }

        protected async Task<byte[]> ExecuteGetBytesAsync(IFlurlRequest request, CancellationToken cancellationToken)
        {
            return await RetryPolicy.ExecuteAsync(() => request.GetBytesAsync(cancellationToken: cancellationToken));
        }

        protected async Task<TResult> ExecutePutAsync<TResult>(IFlurlRequest request, object entity, CancellationToken cancellationToken)
        {
            return await RetryPolicy.ExecuteAsync(() => request
                .PutJsonAsync(entity, cancellationToken: cancellationToken)
                .ReceiveJson<TResult>());
        }

        protected async Task<TResult> ExecuteDeleteAsync<TResult>(IFlurlRequest request, CancellationToken cancellationToken)
        {
            return await RetryPolicy.ExecuteAsync(() => request
                .DeleteAsync(cancellationToken: cancellationToken)
                .ReceiveJson<TResult>());
        }


        protected async Task ExecuteDeleteAsync(IFlurlRequest request, CancellationToken cancellationToken)
        {
            await RetryPolicy.ExecuteAsync(() => request
                .DeleteAsync(cancellationToken: cancellationToken));
        }

        protected async Task ExecuteDeleteAsync(IFlurlRequest request, object entity, CancellationToken cancellationToken)
        {
            await RetryPolicy.ExecuteAsync(() => request
                .SendJsonAsync(HttpMethod.Delete, entity, cancellationToken: cancellationToken));
        }

        protected async Task<TResult> ExecutePostStreamAsync<TResult>(IFlurlRequest request, Action<CapturedMultipartContent> action, CancellationToken cancellationToken)
        {
            return await request
                .PostMultipartAsync(action, cancellationToken: cancellationToken)
                .ReceiveJson<TResult>();
        }

        protected async Task ExecutePostStreamAsync(IFlurlRequest request, Action<CapturedMultipartContent> action, CancellationToken cancellationToken)
        {
            await request
                .PostMultipartAsync(action, cancellationToken: cancellationToken);
        }

        protected async Task<byte[]> ExecutePostAndReceiveBytesAsync(IFlurlRequest request, object entity, CancellationToken cancellationToken)
        {
            return await RetryPolicy.ExecuteAsync(() => request.PostJsonAsync(entity, cancellationToken: cancellationToken).ReceiveBytes());
        }

        protected async Task<Stream> ExecutePostAndReceiveStreamAsync(IFlurlRequest request, object entity, CancellationToken cancellationToken)
        {
            return await RetryPolicy.ExecuteAsync(() => request.PostJsonAsync(entity, cancellationToken: cancellationToken).ReceiveStream());
        }

    }
}