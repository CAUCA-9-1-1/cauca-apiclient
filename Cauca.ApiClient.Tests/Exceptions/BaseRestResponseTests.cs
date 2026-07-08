using System.Net.Http;

namespace Cauca.ApiClient.Tests.Exceptions
{
    public abstract class BaseRestResponseTests
    {
        protected static HttpResponseMessage GetResponse(System.Net.HttpStatusCode code, string headerName)
        {
            var response = new HttpResponseMessage();
            response.Headers.Add(headerName, "True");
            response.StatusCode = code;
            return response;
        }
    }
}