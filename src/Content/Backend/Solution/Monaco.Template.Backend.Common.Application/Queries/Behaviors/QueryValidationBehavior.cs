using FluentValidation;
using MediatR;

namespace Monaco.Template.Backend.Common.Application.Queries.Behaviors;

public class QueryValidationBehavior<TQuery, TResult> : IPipelineBehavior<TQuery, QueryResult<TResult>>
	where TQuery : IRequest<QueryResult<TResult>>
{
	private readonly IEnumerable<IValidator<TQuery>> _validators;

	public QueryValidationBehavior(IEnumerable<IValidator<TQuery>> validators)
	{
		_validators = validators;
	}

	public async Task<QueryResult<TResult>> Handle(TQuery request,
												   RequestHandlerDelegate<QueryResult<TResult>> next,
												   CancellationToken cancellationToken)
	{
		if (!_validators.Any())
			return await next(cancellationToken);
		
		var context = new ValidationContext<TQuery>(request);
		var failures = (await Task.WhenAll(_validators.Select(v => v.ValidateAsync(context, cancellationToken))))
					   .SelectMany(r => r.Errors)
					   .Where(f => f is not null)
					   .ToList();

		return failures.Count > 0
				   ? QueryResult<TResult>.ValidationFailure(new(failures))
				   : await next(cancellationToken);
	}
}