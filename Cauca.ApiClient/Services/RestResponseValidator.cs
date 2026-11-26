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
        if (code is not null && !IsSuccess(code.Value))
            throw new UnexpectedStatusCodeApiException(url, code.Value, exception, body);
        throw exception;
    }

    internal void ThrowExceptionForApiHttpException(ApiHttpException exception)
    {
        if (exception.IsResponseBodyNotReceived)
            throw new ResponseBodyNotReceivedApiException(exception.RequestUri, exception.StatusCode!.Value, exception);

        ThrowExceptionForStatusCode(exception.RequestUri, exception.AnswerReceived, exception.StatusCode, exception, exception.ResponseBody);
    }

    private static bool IsSuccess(HttpStatusCode code)
    {
        return (int)code is >= 200 and <= 299;
    }
}