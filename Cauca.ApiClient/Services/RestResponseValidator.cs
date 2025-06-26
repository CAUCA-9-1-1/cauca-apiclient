using System;
using System.Net;
using Cauca.ApiClient.Exceptions;

namespace Cauca.ApiClient.Services;

public class RestResponseValidator
{
    public void ThrowExceptionForStatusCode(string url, bool answerReceived, HttpStatusCode? code, Exception exception, string body = null)
    {
        if (code == HttpStatusCode.NotFound)
            throw new NotFoundApiException(url, exception, body);
        if (code == HttpStatusCode.BadRequest)
            throw new BadParameterApiException(url, exception, body);
        if (code == HttpStatusCode.Unauthorized)
            throw new UnauthorizedApiException(url, exception);
        if (code == HttpStatusCode.Forbidden)
            throw new ForbiddenApiException(url, exception, body);
        if (code == HttpStatusCode.InternalServerError)
            throw new InternalErrorApiException(url, exception, body);
        if (!answerReceived)
            throw new NoResponseApiException(exception);
        throw exception;
    }        
}