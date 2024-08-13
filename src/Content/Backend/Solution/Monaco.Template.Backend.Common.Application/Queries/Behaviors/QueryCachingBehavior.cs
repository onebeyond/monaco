using MediatR;
using Monaco.Template.Backend.Common.Application.Queries.Contracts;

namespace Monaco.Template.Backend.Common.Application.Queries.Behaviors;

public sealed class QueryCachingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : ICachedQuery
{
	private readonly ICachedQueryService _cachedQueryService;

	public QueryCachingBehavior(ICachedQueryService cachedQueryService)
	{
		_cachedQueryService = cachedQueryService;
	}

	public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
		=> _cachedQueryService.GetOrCreateAsync(request.CacheKey,
												_ => next(),
												request.Expiration,
												cancellationToken)!;
}