using MediatR;
using Polly;
using Polly.Registry;

namespace Monaco.Template.Common.Application.Commands.Behaviors;

public class ConcurrencyExceptionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : IRequest<TResponse>
{
	private readonly IAsyncPolicy<TResponse> _dbConcurrentRetryPolicy;

	public ConcurrencyExceptionBehavior(IReadOnlyPolicyRegistry<string> policyRegistry) =>
		_dbConcurrentRetryPolicy = policyRegistry.Get<IAsyncPolicy<TResponse>>(Policies.Policies.DbConcurrentExceptionPolicyKey);

	public Task<TResponse> Handle(TRequest request,
								  RequestHandlerDelegate<TResponse> next,
								  CancellationToken cancellationToken) =>
		_dbConcurrentRetryPolicy.ExecuteAsync(() => next());
}