using FluentValidation;
using MediatR;
using Monaco.Template.Backend.Common.Application.Validators.Extensions;

namespace Monaco.Template.Backend.Common.Application.Commands.Behaviors;

/// <summary>
/// Behavior to perform validations of Commands that do not return any other data in the CommandResult
/// </summary>
/// <typeparam name="TCommand">The type of the Command to process</typeparam>
public class CommandValidationBehavior<TCommand> : IPipelineBehavior<TCommand, CommandResult>
	where TCommand : CommandBase
{
	protected readonly IValidator<TCommand> Validator;

	/// <summary>
	/// Behavior to perform validations of Commands that do not return any other data in the CommandResult
	/// </summary>
	/// <typeparam name="TCommand">The type of the Command to process</typeparam>
	public CommandValidationBehavior(IValidator<TCommand> validator)
	{
		Validator = validator;
	}

	public virtual async Task<CommandResult> Handle(TCommand request, RequestHandlerDelegate<CommandResult> next, CancellationToken cancellationToken)
	{
		var validationResult = await Validator.ValidateAsync(request, cancellationToken);
		if (!validationResult.IsValid)
			return CommandResult.ValidationFailed(validationResult);

		return await next();
	}
}

/// <summary>
/// Behavior to perform validations on Commands that return data in the CommandResult
/// </summary>
/// <typeparam name="TCommand">The type of Command to process</typeparam>
/// <typeparam name="TResult">The type of data to return along with the CommandResult</typeparam>
public class CommandValidationBehavior<TCommand, TResult> : IPipelineBehavior<TCommand, CommandResult<TResult?>>
	where TCommand : CommandBase<TResult?>
{
	protected readonly IValidator<TCommand> Validator;

	/// <summary>
	/// Behavior to perform validations on Commands that return data in the CommandResult
	/// </summary>
	/// <typeparam name="TCommand">The type of Command to process</typeparam>
	/// <typeparam name="TResult">The type of data to return along with the CommandResult</typeparam>
	public CommandValidationBehavior(IValidator<TCommand> validator)
	{
		Validator = validator;
	}

	public virtual async Task<CommandResult<TResult?>> Handle(TCommand request,
															  RequestHandlerDelegate<CommandResult<TResult?>> next,
															  CancellationToken cancellationToken)
	{
		var validationResult = await Validator.ValidateAsync(request, cancellationToken);
		if (!validationResult.IsValid)
			return CommandResult<TResult?>.ValidationFailed(validationResult, default);

		return await next();
	}
}

/// <summary>
/// Behavior to validate the existence of the entity represented by the Command Id.
/// </summary>
/// <typeparam name="TCommand">The type of the Command to process</typeparam>
public class CommandValidationExistsBehavior<TCommand> : IPipelineBehavior<TCommand, CommandResult>
	where TCommand : CommandBase
{
	protected readonly IValidator<TCommand> Validator;

	/// <summary>
	/// Behavior to validate the existence of the entity represented by the Command Id.
	/// </summary>
	/// <typeparam name="TCommand">The type of the Command to process</typeparam>
	public CommandValidationExistsBehavior(IValidator<TCommand> validator)
	{
		Validator = validator;
	}

	public virtual async Task<CommandResult> Handle(TCommand request,
													RequestHandlerDelegate<CommandResult> next,
													CancellationToken cancellationToken)
	{
		var validationResult = await Validator.ValidateAsync(request, options => options.IncludeRuleSets(ValidatorsExtensions.ExistsRulesetName), cancellationToken);
		if (!validationResult.IsValid)
			return CommandResult.NotFound();

		return await next();
	}
}

/// <summary>
/// Behavior to validate the existence of the entity represented by the Command Id.
/// </summary>
/// <typeparam name="TCommand">The type of the Command to process</typeparam>
/// <typeparam name="TResult">The type of data to return along with the CommandResult</typeparam>
public class CommandValidationExistsBehavior<TCommand, TResult> : IPipelineBehavior<TCommand, CommandResult<TResult?>>
	where TCommand : CommandBase<TResult?>
{
	protected readonly IValidator<TCommand> Validator;

	/// <summary>
	/// Behavior to validate the existence of the entity represented by the Command Id.
	/// </summary>
	/// <typeparam name="TCommand">The type of the Command to process</typeparam>
	/// <typeparam name="TResult">The type of data to return along with the CommandResult</typeparam>
	public CommandValidationExistsBehavior(IValidator<TCommand> validator)
	{
		Validator = validator;
	}

	public virtual async Task<CommandResult<TResult?>> Handle(TCommand request,
															  RequestHandlerDelegate<CommandResult<TResult?>> next,
															  CancellationToken cancellationToken)
	{
		var validationResult = await Validator.ValidateAsync(request,
															 options => options.IncludeRuleSets(ValidatorsExtensions.ExistsRulesetName),
															 cancellationToken);
		if (!validationResult.IsValid)
			return CommandResult<TResult?>.NotFound();

		return await next();
	}
}