using Flurl.Http;
using System;
using System.Threading.Tasks;

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
        return default(T);
    }

    public async Task<string> GetResponseStringAsync()
    {
        if (InnerException is FlurlHttpException flurlException)
            return await flurlException.GetResponseStringAsync();
        return null;
    }
}