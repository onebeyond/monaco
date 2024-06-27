using Microsoft.Extensions.Primitives;
using Monaco.Template.Backend.Common.Application.Queries.Contracts;

namespace Monaco.Template.Backend.Common.Application.Queries;

public abstract record CachedQueryBase<T>(IEnumerable<KeyValuePair<string, StringValues>> QueryString) : QueryBase<T>(QueryString), ICachedQuery<T>
{
	public abstract string CacheKey { get; }
	public virtual TimeSpan? Expiration => null;
}