using System.Threading.Tasks;

namespace Cauca.ApiClient.Services.Interfaces
{
    public interface IBaseService
    {
        Task<TResult> PostAsync<TResult>(string url, object entity, object? headers = null);
        Task<TResult> PutAsync<TResult>(string url, object entity, object? headers = null);
        Task<TResult> DeleteAsync<TResult>(string url, object? headers = null);
        Task<TResult> GetAsync<TResult>(string url, object? headers = null);
    }
}