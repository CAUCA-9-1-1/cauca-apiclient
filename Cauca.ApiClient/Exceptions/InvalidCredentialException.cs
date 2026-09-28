using System;

namespace Cauca.ApiClient.Exceptions;

public class InvalidCredentialException : Exception
{
    public InvalidCredentialException(string userName, Exception innerException)
        : base($"Credential are invalid for username '{userName}'.", innerException)
    {
    }

    internal InvalidCredentialException(Exception innerException)
        : base("The configured external system credentials were rejected.", innerException)
    {
    }
}
