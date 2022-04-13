using Flurl.Http;
using System;
using System.Threading.Tasks;

namespace Cauca.ApiClient.Exceptions
{
	public abstract class ApiClientException : Exception
	{
		protected ApiClientException(string message, Exception innerException) : base(message, innerException)
		{
		}

	    protected ApiClientException(string message) : base(message)
	    {
	    }

		public async Task<T> GetResponseAsync<T>()
        {
			if (InnerException is FlurlHttpException flurlException)
				return await flurlException.GetResponseJsonAsync<T>();			
			return default(T);
        }

		public async Task<string> GetResponseStringAsync()
		{
			if (InnerException is FlurlHttpException flurlException)
				return await flurlException.GetResponseStringAsync();
			return null;
		}
	}
}