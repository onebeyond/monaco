using Monaco.Template.Backend.Common.Application.Queries.Contracts;

namespace Monaco.Template.Backend.Common.Application.Queries;

public abstract record CachedQueryByIdBase<T>(Guid Id) : QueryByIdBase<T>(Id), ICachedQuery<T>
{
	public abstract string CacheKey { get; }
	public virtual TimeSpan? Expiration => null;
}