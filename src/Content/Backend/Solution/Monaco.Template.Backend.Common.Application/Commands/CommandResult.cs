using FluentValidation.Results;
using Monaco.Template.Backend.Common.Application.Commands.Contracts;

namespace Monaco.Template.Backend.Common.Application.Commands;

public class CommandResult<T> : CommandResult, ICommandResult<T>
{
	public CommandResult(ValidationResult validationResult, bool itemNotFound, T result) : base(validationResult, itemNotFound)
	{
		Result = result;
	}

	public CommandResult(ValidationResult validationResult, T result) : base(validationResult)
	{
		Result = result;
	}

	public CommandResult(bool itemNotFound, T result) : base(itemNotFound)
	{
		Result = result;
	}

	public CommandResult(T result)
	{
		Result = result;
	}

	public T Result { get; set; }
}

public class CommandResult : ICommandResult
{
	public CommandResult(ValidationResult validationResult, bool itemNotFound)
	{
		ValidationResult = validationResult;
		ItemNotFound = itemNotFound;
	}

	public CommandResult(bool itemNotFound)
	{
		ValidationResult = new ValidationResult();
		ItemNotFound = itemNotFound;
	}

	public CommandResult(ValidationResult validationResult)
	{
		ValidationResult = validationResult;
		ItemNotFound = false;
	}

	public CommandResult()
	{
		ValidationResult = new ValidationResult();
		ItemNotFound = false;
	}

	public ValidationResult ValidationResult { get; set; }
	public bool ItemNotFound { get; set; }
}