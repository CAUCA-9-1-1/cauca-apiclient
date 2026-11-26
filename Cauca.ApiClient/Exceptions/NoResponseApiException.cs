using System;

namespace Cauca.ApiClient.Exceptions;

public class NoResponseApiException : ApiClientException
{
    private const string DefaultMessage = "API didn't return an answer in a timely manner.";

    public NoResponseApiException(Exception innerException = null)
        : this(DefaultMessage, innerException)
    {
    }

    protected NoResponseApiException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
