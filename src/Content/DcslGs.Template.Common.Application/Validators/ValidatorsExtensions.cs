using System.Linq.Expressions;
using FluentValidation;
using DcslGs.Template.Common.Application.Commands;
using DcslGs.Template.Common.Domain.Model;
using DcslGs.Template.Common.Infrastructure.Context;
using DcslGs.Template.Common.Infrastructure.Context.Extensions;

namespace DcslGs.Template.Common.Application.Validators;

public static class ValidatorsExtensions
{
    public static readonly string ExistsRulesetName = "Exists";

    public static void CheckIfExists<T, TEntity>(this AbstractValidator<T> validator, BaseDbContext dbContext) where T : CommandBase
                                                                                                               where TEntity : Entity
    {
        validator.RuleSet(ExistsRulesetName, () => validator.RuleFor(x => x.Id).MustExistAsync<T, TEntity>(dbContext));
    }

    public static void CheckIfExists<T, TEntity, TResult>(this AbstractValidator<T> validator, BaseDbContext dbContext) where T : CommandBase<TResult>
                                                                                                                        where TEntity : Entity
    {
        validator.RuleSet(ExistsRulesetName, () => validator.RuleFor(x => x.Id).MustExistAsync<T, TEntity, TResult>(dbContext));
    }

    public static void CheckIfExists<T>(this AbstractValidator<T> validator,
                                        Func<Guid, CancellationToken, Task<bool>> predicate) where T : CommandBase
    {
        validator.RuleSet(ExistsRulesetName, () => validator.RuleFor(x => x.Id).MustAsync(predicate));
    }

    public static void CheckIfExists<T, TResult>(this AbstractValidator<T> validator,
                                                 Func<Guid, CancellationToken, Task<bool>> predicate) where T : CommandBase<TResult>
    {
        validator.RuleSet(ExistsRulesetName, () => validator.RuleFor(x => x.Id).MustAsync(predicate));
    }

    public static void CheckIfExists<T>(this AbstractValidator<T> validator,
                                        Func<T, Guid, CancellationToken, Task<bool>> predicate) where T : CommandBase
    {
        validator.RuleSet(ExistsRulesetName, () => validator.RuleFor(x => x.Id).MustAsync(predicate));
    }

    public static void CheckIfExists<T, TResult>(this AbstractValidator<T> validator,
                                                 Func<T, Guid, CancellationToken, Task<bool>> predicate) where T : CommandBase<TResult>
    {
        validator.RuleSet(ExistsRulesetName, () => validator.RuleFor(x => x.Id).MustAsync(predicate));
    }

    public static void CheckIfExists<T, TProp>(this AbstractValidator<T> validator,
                                               Expression<Func<T, TProp>> selector,
                                               Func<TProp, CancellationToken, Task<bool>> predicate) where T : CommandBase
    {
        validator.RuleSet(ExistsRulesetName, () => validator.RuleFor(selector).MustAsync(predicate));
    }

    public static void CheckIfExists<T, TProp>(this AbstractValidator<T> validator,
                                               Expression<Func<T, TProp>> selector,
                                               Func<T, TProp, CancellationToken, Task<bool>> predicate) where T : CommandBase
    {
        validator.RuleSet(ExistsRulesetName, () => validator.RuleFor(selector).MustAsync(predicate));
    }

    public static void CheckIfExists<T, TProp>(this AbstractValidator<T> validator,
                                               Expression<Func<T, TProp>> selector,
                                               Func<T,
                                                   TProp,
                                                   ValidationContext<T>,
                                                   CancellationToken,
                                                   Task<bool>> predicate) where T : CommandBase
    {
        validator.RuleSet(ExistsRulesetName, () => validator.RuleFor(selector).MustAsync(predicate));
    }

    public static void CheckIfExists<T, TProp, TResult>(this AbstractValidator<T> validator,
                                                        Expression<Func<T, TProp>> selector,
                                                        Func<TProp, CancellationToken, Task<bool>> predicate) where T : CommandBase<TResult>
    {
        validator.RuleSet(ExistsRulesetName, () => validator.RuleFor(selector).MustAsync(predicate));
    }

    public static void CheckIfExists<T, TProp, TResult>(this AbstractValidator<T> validator,
                                                        Expression<Func<T, TProp>> selector,
                                                        Func<T,
                                                            TProp,
                                                            CancellationToken,
                                                            Task<bool>> predicate) where T : CommandBase<TResult>
    {
        validator.RuleSet(ExistsRulesetName, () => validator.RuleFor(selector).MustAsync(predicate));
    }

    public static void CheckIfExists<T, TProp, TResult>(this AbstractValidator<T> validator,
                                                        Expression<Func<T, TProp>> selector,
                                                        Func<T,
                                                            TProp,
                                                            ValidationContext<T>,
                                                            CancellationToken,
                                                            Task<bool>> predicate) where T : CommandBase<TResult>
    {
        validator.RuleSet(ExistsRulesetName, () => validator.RuleFor(selector).MustAsync(predicate));
    }

    public static IRuleBuilderOptions<T, Guid> MustExistAsync<T, TEntity>(this IRuleBuilder<T, Guid> ruleBuilder,
                                                                          BaseDbContext dbContext) where T : CommandBase
                                                                                                   where TEntity : Entity
    {
        return ruleBuilder.MustAsync(dbContext.ExistsAsync<TEntity>)
                          .WithMessage("The value {PropertyValue} is not valid");
    }

    public static IRuleBuilderOptions<T, Guid?> MustExistAsync<T, TEntity>(this IRuleBuilder<T, Guid?> ruleBuilder,
                                                                           BaseDbContext dbContext) where T : CommandBase
                                                                                                    where TEntity : Entity
    {
        return ruleBuilder.MustAsync(async (id, ct) => await dbContext.ExistsAsync<TEntity>(x => x.Id == id.Value, ct))
                          .WithMessage("The value {PropertyValue} is not valid");
    }

    public static IRuleBuilderOptions<T, Guid?> MustExistAsync<T, TEntity, TResult>(this IRuleBuilder<T, Guid?> ruleBuilder,
                                                                                    BaseDbContext dbContext) where T : CommandBase<TResult>
                                                                                                             where TEntity : Entity
    {
        return ruleBuilder.MustAsync(async (id, ct) => await dbContext.ExistsAsync<TEntity>(x => x.Id == id.Value, ct))
                          .WithMessage("The value {PropertyValue} is not valid");
    }

    public static IRuleBuilderOptions<T, Guid> MustExistAsync<T, TEntity, TResult>(this IRuleBuilder<T, Guid> ruleBuilder,
                                                                                   BaseDbContext dbContext) where T : CommandBase<TResult>
                                                                                                            where TEntity : Entity
    {
        return ruleBuilder.MustAsync(dbContext.ExistsAsync<TEntity>)
                          .WithMessage("The value {PropertyValue} is not valid");
    }
}