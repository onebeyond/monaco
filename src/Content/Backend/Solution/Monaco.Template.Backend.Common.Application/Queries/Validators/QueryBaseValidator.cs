using FluentValidation;
using Monaco.Template.Backend.Common.Infrastructure.Context.Extensions;

namespace Monaco.Template.Backend.Common.Application.Queries.Validators;

public sealed class QueryBaseValidator<TQuery, T, TEntity> : AbstractValidator<TQuery>
	where TQuery : QueryBase<T, TEntity>
	where TEntity : class
{
	public QueryBaseValidator()
	{
		RuleForEach(x => x.Sort)
			.Must((query, sortField) => sortField is null ||
										query.GetSortingMappedFields()
											 .ContainsKey(sortField.TrimStart('-')))
			.WithMessage((_, sortField) => $"Sort field '{sortField?.TrimStart('-')}' is not a valid sortable field.");

		RuleFor(x => x.QueryParams)
			.Custom((queryParams, context) =>
					{
						if (context.InstanceToValidate.GetFilteringMappedFields().Count == 0)
							return;
						
						var mappedFields = context.InstanceToValidate
												  .GetFilteringMappedFields()
												  .ToDictionary(StringComparer.OrdinalIgnoreCase);

						foreach (var param in queryParams.Where(p => mappedFields.ContainsKey(p.Key)))
						{
							var type = FilterExtensions.GetBodyExpression(mappedFields[param.Key]).Type;

							foreach (var value in param.Value
													   .Where(v => v is not null &&
																   !FilterExtensions.ValidateDataType(v, type)))
								context.AddFailure(param.Key, $"Value '{value}' is not valid for filter field '{param.Key}' which expects type '{type.Name}'.");
						}
					});
	}
}