using System;
using System.Text.Json;
using System.Threading.Tasks;
using Cauca.ApiClient.Services;
using Flurl.Http;

namespace Cauca.ApiClient.Exceptions;

public abstract class ApiClientException : Exception
{
    public string Body { get; set; }
    public string ResponseBody { get; set; }

    protected ApiClientException(string message, Exception innerException, string body = null, string responseBody = null) : base(message, innerException)
    {
        Body = body;
        ResponseBody = responseBody;
    }

    protected ApiClientException(string message) : base(message)
    {
    }

    public async Task<T> GetResponseAsync<T>()
    {
        if (InnerException is FlurlHttpException flurlException)
            return await flurlException.GetResponseJsonAsync<T>();

        await Task.CompletedTask;
        if (InnerException is ApiHttpException apiException && !string.IsNullOrWhiteSpace(apiException.ResponseBody))
            return JsonSerializer.Deserialize<T>(apiException.ResponseBody);
        return default(T);
    }

    public async Task<string> GetResponseStringAsync()
    {
        if (InnerException is FlurlHttpException flurlException)
            return await flurlException.GetResponseStringAsync();

        await Task.CompletedTask;
        if (InnerException is ApiHttpException apiException)
            return apiException.ResponseBody;
        return null;
    }
}