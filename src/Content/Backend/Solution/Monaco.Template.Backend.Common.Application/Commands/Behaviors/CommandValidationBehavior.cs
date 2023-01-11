using FluentValidation;
using MediatR;
using Monaco.Template.Backend.Common.Application.Commands.Contracts;
using Monaco.Template.Backend.Common.Application.Validators.Extensions;

namespace Monaco.Template.Backend.Common.Application.Commands.Behaviors;

/// <summary>
/// Behavior to perform validations of Commands that do not return any other data in the CommandResult
/// </summary>
/// <typeparam name="TCommand">The type of the Command to process</typeparam>
public class CommandValidationBehavior<TCommand> : IPipelineBehavior<TCommand, ICommandResult> where TCommand : CommandBase
{
	protected readonly IValidator<TCommand> Validator;

	public CommandValidationBehavior(IValidator<TCommand> validator)
	{
		Validator = validator;
	}

	public virtual async Task<ICommandResult> Handle(TCommand request, RequestHandlerDelegate<ICommandResult> next, CancellationToken cancellationToken)
	{
		var validationResult = await Validator.ValidateAsync(request, cancellationToken);
		if (!validationResult.IsValid)
			return new CommandResult(validationResult);

		return await next();
	}
}

/// <summary>
/// Behavior to perform validations on Commands that return data in the CommandResult
/// </summary>
/// <typeparam name="TCommand">The type of Command to process</typeparam>
/// <typeparam name="TResult">The type of data to return along with the CommandResult</typeparam>
public class CommandValidationBehavior<TCommand, TResult> : IPipelineBehavior<TCommand, ICommandResult<TResult?>> where TCommand : CommandBase<TResult?>
{
	protected readonly IValidator<TCommand> Validator;

	public CommandValidationBehavior(IValidator<TCommand> validator)
	{
		Validator = validator;
	}

	public virtual async Task<ICommandResult<TResult?>> Handle(TCommand request,
															   RequestHandlerDelegate<ICommandResult<TResult?>> next,
															   CancellationToken cancellationToken)
	{
		var validationResult = await Validator.ValidateAsync(request, cancellationToken);
		if (!validationResult.IsValid)
			return new CommandResult<TResult?>(validationResult, default);

		return await next();
	}
}

/// <summary>
/// Behavior to validate the existance of the entity represented by the Command Id.
/// </summary>
/// <typeparam name="TCommand">The type of the Command to process</typeparam>
public class CommandValidationExistsBehavior<TCommand> : IPipelineBehavior<TCommand, ICommandResult> where TCommand : CommandBase
{
	protected readonly IValidator<TCommand> Validator;

	public CommandValidationExistsBehavior(IValidator<TCommand> validator)
	{
		Validator = validator;
	}

	public virtual async Task<ICommandResult> Handle(TCommand request, RequestHandlerDelegate<ICommandResult> next, CancellationToken cancellationToken)
	{
		var validationResult = await Validator.ValidateAsync(request, options => options.IncludeRuleSets(ValidatorsExtensions.ExistsRulesetName), cancellationToken);
		if (!validationResult.IsValid)
			return new CommandResult(true);

		return await next();
	}
}

/// <summary>
/// Behavior to validate the existance of the entity represented by the Command Id.
/// </summary>
/// <typeparam name="TCommand">The type of the Command to process</typeparam>
/// <typeparam name="TResult">The type of data to return along with the CommandResult</typeparam>
public class CommandValidationExistsBehavior<TCommand, TResult> : IPipelineBehavior<TCommand, ICommandResult<TResult?>> where TCommand : CommandBase<TResult?>
{
	protected readonly IValidator<TCommand> Validator;

	public CommandValidationExistsBehavior(IValidator<TCommand> validator)
	{
		Validator = validator;
	}

	public virtual async Task<ICommandResult<TResult?>> Handle(TCommand request,
															   RequestHandlerDelegate<ICommandResult<TResult?>> next,
															   CancellationToken cancellationToken)
	{
		var validationResult = await Validator.ValidateAsync(request,
															 options => options.IncludeRuleSets(ValidatorsExtensions.ExistsRulesetName),
															 cancellationToken);
		if (!validationResult.IsValid)
			return new CommandResult<TResult?>(true, default);

		return await next();
	}
}