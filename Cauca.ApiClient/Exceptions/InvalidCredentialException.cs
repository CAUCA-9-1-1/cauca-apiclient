using System;

namespace Cauca.ApiClient.Exceptions
{
    public class InvalidCredentialException : Exception
    {
        public InvalidCredentialException(string userName, Exception innerException) 
            : base($"Credential are invalid for username '{userName}'.", innerException)
        {
        }
    }
}