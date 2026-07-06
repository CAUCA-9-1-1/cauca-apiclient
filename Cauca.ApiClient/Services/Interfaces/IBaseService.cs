using System.Threading;
using System.Threading.Tasks;

namespace Cauca.ApiClient.Services.Interfaces;

public interface IBaseService
{
    Task<TResult> PostAsync<TResult>(string url, object entity, CancellationToken cancellationToken = default);
    Task<TResult> PutAsync<TResult>(string url, object entity, CancellationToken cancellationToken = default);
    Task<TResult> DeleteAsync<TResult>(string url, CancellationToken cancellationToken = default);
    Task<TResult> GetAsync<TResult>(string url, CancellationToken cancellationToken = default);
}