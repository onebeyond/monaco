using FluentValidation;
using Monaco.Template.Backend.Common.Application.Commands;
using Monaco.Template.Backend.Common.Domain.Model;
using Monaco.Template.Backend.Common.Infrastructure.Context;
using Monaco.Template.Backend.Common.Infrastructure.Context.Extensions;
using System.Linq.Expressions;

namespace Monaco.Template.Backend.Common.Application.Validators.Extensions;

public static class ValidatorsExtensions
{
	public static readonly string ExistsRulesetName = "Exists";

	public static void CheckIfExists<TCommand, TEntity>(this AbstractValidator<TCommand> validator, BaseDbContext dbContext) where TCommand : CommandBase
																															 where TEntity : Entity =>
		validator.RuleSet(ExistsRulesetName, () => validator.RuleFor(x => x.Id).MustExistAsync<TCommand, TEntity>(dbContext));

	public static void CheckIfExists<TCommand, TEntity, TCommandResult>(this AbstractValidator<TCommand> validator, BaseDbContext dbContext) where TCommand : CommandBase<TCommandResult>
																																			 where TEntity : Entity =>
		validator.RuleSet(ExistsRulesetName, () => validator.RuleFor(x => x.Id).MustExistAsync<TCommand, TEntity, TCommandResult>(dbContext));

	public static void CheckIfExists<TCommand>(this AbstractValidator<TCommand> validator,
											   Func<Guid, CancellationToken, Task<bool>> predicate) where TCommand : CommandBase =>
		validator.RuleSet(ExistsRulesetName, () => validator.RuleFor(x => x.Id)
															.MustAsync(predicate));

	public static void CheckIfExists<TCommand, TCommandResult>(this AbstractValidator<TCommand> validator,
															   Func<Guid, CancellationToken, Task<bool>> predicate) where TCommand : CommandBase<TCommandResult> =>
		validator.RuleSet(ExistsRulesetName, () => validator.RuleFor(x => x.Id)
															.MustAsync(predicate));

	public static void CheckIfExists<TCommand>(this AbstractValidator<TCommand> validator,
											   Func<TCommand, Guid, CancellationToken, Task<bool>> predicate) where TCommand : CommandBase =>
		validator.RuleSet(ExistsRulesetName, () => validator.RuleFor(x => x.Id)
															.MustAsync(predicate));

	public static void CheckIfExists<TCommand, TCommandResult>(this AbstractValidator<TCommand> validator,
															   Func<TCommand, Guid, CancellationToken, Task<bool>> predicate) where TCommand : CommandBase<TCommandResult> =>
		validator.RuleSet(ExistsRulesetName, () => validator.RuleFor(x => x.Id)
															.MustAsync(predicate));

	public static void CheckIfExists<TCommand, TPropertyType>(this AbstractValidator<TCommand> validator,
															  Expression<Func<TCommand, TPropertyType>> selector,
															  Func<TPropertyType, CancellationToken, Task<bool>> predicate) where TCommand : CommandBase =>
		validator.RuleSet(ExistsRulesetName, () => validator.RuleFor(selector)
															.MustAsync(predicate));

	public static void CheckIfExists<TComomand, TPropertyType>(this AbstractValidator<TComomand> validator,
															   Expression<Func<TComomand, TPropertyType>> selector,
															   Func<TComomand, TPropertyType, CancellationToken, Task<bool>> predicate) where TComomand : CommandBase =>
		validator.RuleSet(ExistsRulesetName, () => validator.RuleFor(selector)
															.MustAsync(predicate));

	public static void CheckIfExists<TCommand, TPropertyType>(this AbstractValidator<TCommand> validator,
															  Expression<Func<TCommand, TPropertyType>> selector,
															  Func<TCommand, TPropertyType, ValidationContext<TCommand>, CancellationToken, Task<bool>> predicate) where TCommand : CommandBase =>
		validator.RuleSet(ExistsRulesetName, () => validator.RuleFor(selector)
															.MustAsync(predicate));

	public static void CheckIfExists<TCommand, TPropertyType, TCommandResult>(this AbstractValidator<TCommand> validator,
																			  Expression<Func<TCommand, TPropertyType>> selector,
																			  Func<TPropertyType, CancellationToken, Task<bool>> predicate) where TCommand : CommandBase<TCommandResult> =>
		validator.RuleSet(ExistsRulesetName, () => validator.RuleFor(selector)
															.MustAsync(predicate));

	public static void CheckIfExists<TCommand, TPropertyType, TResult>(this AbstractValidator<TCommand> validator,
																	   Expression<Func<TCommand, TPropertyType>> selector,
																	   Func<TCommand, TPropertyType, CancellationToken, Task<bool>> predicate) where TCommand : CommandBase<TResult> =>
		validator.RuleSet(ExistsRulesetName, () => validator.RuleFor(selector)
															.MustAsync(predicate));

	public static void CheckIfExists<TCommand, TPropertyType, TResult>(this AbstractValidator<TCommand> validator,
																	   Expression<Func<TCommand, TPropertyType>> selector,
																	   Func<TCommand,
																		   TPropertyType,
																		   ValidationContext<TCommand>,
																		   CancellationToken,
																		   Task<bool>> predicate) where TCommand : CommandBase<TResult> =>
		validator.RuleSet(ExistsRulesetName, () => validator.RuleFor(selector)
															.MustAsync(predicate));

	public static IRuleBuilderOptions<TCommand, Guid> MustExistAsync<TCommand, TEntity>(this IRuleBuilder<TCommand, Guid> ruleBuilder,
																						BaseDbContext dbContext) where TCommand : CommandBase
																												 where TEntity : Entity =>
		ruleBuilder.MustAsync(dbContext.ExistsAsync<TEntity>)
				   .WithMessage("The value {PropertyValue} is not valid");

	public static IRuleBuilderOptions<TCommand, Guid?> MustExistAsync<TCommand, TEntity>(this IRuleBuilder<TCommand, Guid?> ruleBuilder,
																						 BaseDbContext dbContext) where TCommand : CommandBase
																												  where TEntity : Entity =>
		ruleBuilder.MustAsync(async (id, ct) => id.HasValue &&
												await dbContext.ExistsAsync<TEntity>(x => x.Id == id.Value, ct))
				   .WithMessage("The value {PropertyValue} is not valid");

	public static IRuleBuilderOptions<TCommand, Guid?> MustExistAsync<TCommand, TEntity, TResult>(this IRuleBuilder<TCommand, Guid?> ruleBuilder,
																								  BaseDbContext dbContext) where TCommand : CommandBase<TResult>
																														   where TEntity : Entity =>
		ruleBuilder.MustAsync(async (id, ct) => id.HasValue &&
												await dbContext.ExistsAsync<TEntity>(x => x.Id == id.Value, ct))
				   .WithMessage("The value {PropertyValue} is not valid");

	public static IRuleBuilderOptions<TCommand, Guid> MustExistAsync<TCommand, TEntity, TResult>(this IRuleBuilder<TCommand, Guid> ruleBuilder,
																								 BaseDbContext dbContext) where TCommand : CommandBase<TResult>
																														  where TEntity : Entity =>
		ruleBuilder.MustAsync(dbContext.ExistsAsync<TEntity>)
				   .WithMessage("The value {PropertyValue} is not valid");
}