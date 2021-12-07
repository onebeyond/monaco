using FluentValidation;
using MediatR;
using DcslGs.Template.Common.Application.Commands.Contracts;
using DcslGs.Template.Common.Application.Validators;

namespace DcslGs.Template.Common.Application.Commands.Behaviors;

/// <summary>
/// Behavior to perform validations of Commands that do not return any other data in the CommandResult
/// </summary>
/// <typeparam name="TCommand">The type of the Command to process</typeparam>
public class PreCommandProcessorBehavior<TCommand> : IPipelineBehavior<TCommand, ICommandResult> where TCommand : CommandBase
{
    protected readonly IValidator<TCommand> Validator;

    public PreCommandProcessorBehavior(IValidator<TCommand> validator)
    {
        Validator = validator;
    }

    public virtual async Task<ICommandResult> Handle(TCommand request, CancellationToken cancellationToken, RequestHandlerDelegate<ICommandResult> next)
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
public class PreCommandProcessorBehavior<TCommand, TResult> : IPipelineBehavior<TCommand, ICommandResult<TResult>> where TCommand : CommandBase<TResult>
{
    protected readonly IValidator<TCommand> Validator;

    public PreCommandProcessorBehavior(IValidator<TCommand> validator)
    {
        Validator = validator;
    }

    public virtual async Task<ICommandResult<TResult>> Handle(TCommand request, CancellationToken cancellationToken, RequestHandlerDelegate<ICommandResult<TResult>> next)
    {
        var validationResult = await Validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
            return new CommandResult<TResult>(validationResult, default);

        return await next();
    }
}

/// <summary>
/// Behavior to validate the existance of the entity represented by the Command Id.
/// </summary>
/// <typeparam name="TCommand">The type of the Command to process</typeparam>
public class PreCommandProcessorExistsBehavior<TCommand> : IPipelineBehavior<TCommand, ICommandResult> where TCommand : CommandBase
{
    protected readonly IValidator<TCommand> Validator;

    public PreCommandProcessorExistsBehavior(IValidator<TCommand> validator)
    {
        Validator = validator;
    }

    public virtual async Task<ICommandResult> Handle(TCommand request, CancellationToken cancellationToken, RequestHandlerDelegate<ICommandResult> next)
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
public class PreCommandProcessorExistsBehavior<TCommand, TResult> : IPipelineBehavior<TCommand, ICommandResult<TResult>> where TCommand : CommandBase<TResult>
{
    protected readonly IValidator<TCommand> Validator;

    public PreCommandProcessorExistsBehavior(IValidator<TCommand> validator)
    {
        Validator = validator;
    }

    public virtual async Task<ICommandResult<TResult>> Handle(TCommand request, CancellationToken cancellationToken, RequestHandlerDelegate<ICommandResult<TResult>> next)
    {
        var validationResult = await Validator.ValidateAsync(request, options => options.IncludeRuleSets(ValidatorsExtensions.ExistsRulesetName), cancellationToken);
        if (!validationResult.IsValid)
            return new CommandResult<TResult>(true, default);

        return await next();
    }
}