using FluentValidation.Results;

namespace Monaco.Template.Backend.Common.Application.Commands;

public class CommandResult<T> : CommandResult
{
	public CommandResult(ValidationResult validationResult, bool itemNotFound, T result) : base(validationResult, itemNotFound)
	{
		Result = result;
	}

	public T Result { get; set; }

	public static CommandResult<T> Success(T result) =>
		new(new(), false, result);

	public new static CommandResult<T?> NotFound() =>
		new(new(), true, default);

	public static CommandResult<T?> ValidationFailed(ValidationResult validationResult, T? result) =>
		new(validationResult, false, result);
}

public class CommandResult
{
	public CommandResult(ValidationResult validationResult, bool itemNotFound)
	{
		ValidationResult = validationResult;
		ItemNotFound = itemNotFound;
	}

	public ValidationResult ValidationResult { get; set; }
	public bool ItemNotFound { get; set; }

	public static CommandResult Success() =>
		new(new(), false);

	public static CommandResult NotFound() =>
		new(new(), true);
	
	public static CommandResult ValidationFailed(ValidationResult validationResult) =>
		new(validationResult, false);
}