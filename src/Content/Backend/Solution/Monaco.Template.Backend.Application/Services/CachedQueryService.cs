using Microsoft.Extensions.Caching.Memory;
using Monaco.Template.Backend.Common.Application.Queries.Contracts;

namespace Monaco.Template.Backend.Application.Services;

public class CachedQueryService : ICachedQueryService
{
	private static readonly TimeSpan DefaultExpiration = TimeSpan.FromMinutes(5);
	private readonly IMemoryCache _memoryCache;

	public CachedQueryService(IMemoryCache memoryCache)
	{
		_memoryCache = memoryCache;
	}

	public async Task<T?> GetOrCreateAsync<T>(string cacheKey,
											  Func<CancellationToken, Task<T>> factory,
											  TimeSpan? expiration = null,
											  CancellationToken cancellationToken = default)
		=> await _memoryCache.GetOrCreateAsync(cacheKey, entry =>
		{
			entry.SetAbsoluteExpiration(expiration ?? DefaultExpiration);
			return factory(cancellationToken);
		});
}