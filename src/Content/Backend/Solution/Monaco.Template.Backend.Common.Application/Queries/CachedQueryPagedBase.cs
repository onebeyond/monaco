using Microsoft.Extensions.Primitives;
using Monaco.Template.Backend.Common.Application.Queries.Contracts;

namespace Monaco.Template.Backend.Common.Application.Queries;

public abstract record CachedQueryPagedBase<T>(IEnumerable<KeyValuePair<string, StringValues>> QueryString) : QueryPagedBase<T>(QueryString), ICachedQuery<T>
{
	public abstract string CacheKey { get; }
	public virtual TimeSpan? Expiration => null;
}