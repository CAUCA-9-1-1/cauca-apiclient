using System.Net.Http;
using Flurl.Http;

namespace Cauca.ApiClient.Tests.Exceptions
{
    public abstract class BaseRestResponseTests
    {
        protected static FlurlCall GetResponse(System.Net.HttpStatusCode code, string headerName)
        {
            var response = new HttpResponseMessage();
            response.Headers.Add(headerName, "True");
            response.StatusCode = code;
            var call = new FlurlCall { HttpResponseMessage = response };
            call.Response = new FlurlResponse(call);
            return call;
        }
    }
}