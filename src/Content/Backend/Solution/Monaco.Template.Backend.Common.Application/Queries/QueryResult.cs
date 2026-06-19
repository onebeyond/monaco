using FluentValidation.Results;

namespace Monaco.Template.Backend.Common.Application.Queries;

public abstract record QueryResult<T>
{
	public static Success<T> Success(T result) => new(result);
	public static ValidationFailure<T> ValidationFailure(ValidationResult validationResult) => new(validationResult);
	public static NotFound<T> NotFound() => new();
	public static QueryResult<T> SuccessOrNotFound(T? result) =>
		result is not null
			? new Success<T>(result)
			: new NotFound<T>();
}

public sealed record Success<T>(T Result) : QueryResult<T>;
public sealed record NotFound<T> : QueryResult<T>;
public sealed record ValidationFailure<T>(ValidationResult ValidationResult) : QueryResult<T>;