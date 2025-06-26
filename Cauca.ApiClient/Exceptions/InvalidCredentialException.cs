using System;

namespace Cauca.ApiClient.Exceptions;

public class InvalidCredentialException(string userName, Exception innerException)
    : Exception($"Credential are invalid for username '{userName}'.", innerException);