using System.Net.Http;

namespace Cauca.ApiClient.Tests.Helpers;

public sealed class PassThroughHandler : DelegatingHandler
{
    public PassThroughHandler(TestHttpMessageHandler transport)
    {
        InnerHandler = transport;
    }
}
