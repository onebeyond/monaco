using FluentValidation;
using Microsoft.Extensions.Primitives;
using Monaco.Template.Backend.Common.Application.Queries.Contracts;
using Monaco.Template.Backend.Common.Domain.Model;

namespace Monaco.Template.Backend.Common.Application.Queries.Validators;

public abstract class QueryPagedValidatorBase<TQuery> : AbstractValidator<TQuery>
	where TQuery : IPagedQuery
{
	protected QueryPagedValidatorBase()
	{
		RuleFor(x => x.QueryParams)
			.Must(p => GetValues(p, nameof(Pager.Offset)).Count() <= 1)
			.WithMessage($"{nameof(Pager.Offset)} must be specified only once.");

		RuleFor(x => x.QueryParams)
			.Must(p => GetValues(p, nameof(Pager.Limit)).Count() <= 1)
			.WithMessage($"{nameof(Pager.Limit)} must be specified only once.");

		RuleFor(x => x.QueryParams)
			.Must(p => !TryGetInt(p, nameof(Pager.Offset), out var v) ||
					   v >= 0)
			.WithMessage($"{nameof(Pager.Offset)} must be greater than or equal to 0.");

		RuleFor(x => x.QueryParams)
			.Must(p => !TryGetInt(p, nameof(Pager.Limit), out var v) ||
					   v is > 0 and <= IPagedQuery.MaxLimit)
			.WithMessage($"{nameof(Pager.Limit)} must be between 1 and {IPagedQuery.MaxLimit}.");
	}

	protected static IEnumerable<string?> GetValues(IEnumerable<KeyValuePair<string, StringValues>> queryParams,
													string key) =>
		queryParams.Where(x => x.Key.Equals(key, StringComparison.InvariantCultureIgnoreCase))
				   .SelectMany(x => x.Value);

	protected static bool TryGetInt(IEnumerable<KeyValuePair<string, StringValues>> queryParams,
									string key,
									out int value) =>
		int.TryParse(GetValues(queryParams, key).FirstOrDefault(),
					 out value);
}

public sealed class QueryPagedBaseValidator<T> : QueryPagedValidatorBase<QueryPagedBase<T>>;

public sealed class QueryPagedBaseValidator<T, TEntity> : QueryPagedValidatorBase<QueryPagedBase<T, TEntity>>
	where TEntity : class
{
	public QueryPagedBaseValidator()
	{
		RuleForEach(x => x.Sort)
			.Must((query, sortField) => sortField is null ||
										query.GetSortingMappedFields()
											 .ContainsKey(sortField.TrimStart('-')))
			.WithMessage((_, sortField) => $"Sort field '{sortField?.TrimStart('-')}' is not a valid sortable field.");
	}
}