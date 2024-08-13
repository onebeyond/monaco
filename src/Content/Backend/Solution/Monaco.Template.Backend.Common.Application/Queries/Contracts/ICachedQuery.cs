using MediatR;

namespace Monaco.Template.Backend.Common.Application.Queries.Contracts;

public interface ICachedQuery
{
	public abstract string CacheKey { get; }
	public TimeSpan? Expiration { get; }
}

public interface ICachedQuery<T> : IRequest<T>, ICachedQuery;