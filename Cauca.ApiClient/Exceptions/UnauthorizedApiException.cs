using System;

namespace Cauca.ApiClient.Exceptions
{
	public class UnauthorizedApiException : ApiClientException
	{
		public UnauthorizedApiException(string url, Exception innerException = null)
			: base($"API returned a 401 (unauthorized) response for url '{url}'.", innerException)
		{
		}
	}
}