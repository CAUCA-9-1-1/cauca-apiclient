using System;

namespace Cauca.ApiClient.Exceptions;

public class ExpiredRefreshTokenException() : Exception("The refresh token is expired.");