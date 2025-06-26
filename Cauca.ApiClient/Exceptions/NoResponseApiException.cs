using System;

namespace Cauca.ApiClient.Exceptions;

public class NoResponseApiException(Exception innerException = null)
    : ApiClientException("API didn't return an answer in a timely manner.", innerException);