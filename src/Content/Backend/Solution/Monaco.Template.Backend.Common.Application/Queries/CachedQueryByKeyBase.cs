using Monaco.Template.Backend.Common.Application.Queries.Contracts;

namespace Monaco.Template.Backend.Common.Application.Queries;

public abstract record CachedQueryByKeyBase<T, TKey>(TKey Key) : QueryByKeyBase<T, TKey>(Key), ICachedQuery<T>
{
	public abstract string CacheKey { get; }
	public virtual TimeSpan? Expiration => null;
}