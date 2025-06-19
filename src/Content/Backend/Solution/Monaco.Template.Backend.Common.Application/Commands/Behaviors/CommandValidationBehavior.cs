using FluentValidation;
using MediatR;
using Monaco.Template.Backend.Common.Application.Validators.Extensions;

namespace Monaco.Template.Backend.Common.Application.Commands.Behaviors;

/// <summary>
/// Represents a pipeline behavior that validates commands before they are processed by the next handler in the pipeline.
/// </summary>
/// <remarks>This behavior ensures that commands are validated using the provided <see cref="IValidator{T}"/>
/// implementation before they proceed further in the pipeline. If validation fails, the pipeline is short-circuited,
/// and a <see cref="CommandResult"/> indicating validation failure is returned.</remarks>
/// <typeparam name="TCommand">The type of the command to validate. Must inherit from <see cref="CommandBase"/>.</typeparam>
public class CommandValidationBehavior<TCommand> : IPipelineBehavior<TCommand, CommandResult>
	where TCommand : CommandBase
{
	protected readonly IValidator<TCommand> Validator;

	/// <summary>
	/// Initializes a new instance of the <see cref="CommandValidationBehavior{TCommand}"/> class, which enforces
	/// validation for commands using the specified validator.
	/// </summary>
	/// <param name="validator">The validator used to validate instances of <typeparamref name="TCommand"/>. Cannot be <see langword="null"/>.</param>
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
/// Represents a pipeline behavior that validates commands before they are processed by the next handler in the pipeline.
/// </summary>
/// <remarks>This behavior ensures that commands are validated using the provided <see cref="IValidator{T}"/>
/// implementation before they proceed further in the pipeline. If validation fails, the pipeline is short-circuited,
/// and a <see cref="CommandResult{TResult}"/> indicating validation failure is returned.</remarks>
/// <typeparam name="TCommand">The type of the command to validate. Must inherit from <see cref="CommandBase{TResult}"/>.</typeparam>
/// <typeparam name="TResult">The type of the result returned by the command, encapsulated in a <see cref="CommandResult{TResult}"/>.</typeparam>
public class CommandValidationBehavior<TCommand, TResult> : IPipelineBehavior<TCommand, CommandResult<TResult?>>
	where TCommand : CommandBase<TResult?>
{
	protected readonly IValidator<TCommand> Validator;

	/// <summary>
	/// Initializes a new instance of the <see cref="CommandValidationBehavior{TCommand}"/> class, which enforces
	/// validation for commands using the specified validator.
	/// </summary>
	/// <param name="validator">The validator used to validate instances of <typeparamref name="TCommand"/>. Cannot be <see langword="null"/>.</param>
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
/// Represents a pipeline behavior that validates the existence of an entity associated with the command's identifier
/// before proceeding to the next handler.
/// </summary>
/// <remarks>This behavior uses a validator to ensure that the entity represented by the command's identifier
/// exists.  If the validation fails, the pipeline short-circuits and returns a "Not Found" result.</remarks>
/// <typeparam name="TCommand">The type of the command being processed. Must inherit from <see cref="CommandBase"/>.</typeparam>
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
/// Represents a pipeline behavior that validates the existence of an entity associated with the command's identifier
/// before proceeding to the next handler.
/// </summary>
/// <remarks>This behavior uses a validator to ensure that the entity represented by the command's identifier
/// exists.  If the validation fails, the pipeline short-circuits and returns a "Not Found" result.</remarks>
/// <typeparam name="TCommand">The type of the command being processed. Must inherit from <see cref="CommandBase{TResult}"/>.</typeparam>
/// <typeparam name="TResult">The type of the result data returned by the command.</typeparam>
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