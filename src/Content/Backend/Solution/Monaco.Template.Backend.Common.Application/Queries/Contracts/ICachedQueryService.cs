namespace Monaco.Template.Backend.Common.Application.Queries.Contracts;

public interface ICachedQueryService
{
	Task<T?> GetOrCreateAsync<T>(string key,
								 Func<CancellationToken, Task<T>> factory,
								 TimeSpan? expiration = null,
								 CancellationToken cancellationToken = default);
}