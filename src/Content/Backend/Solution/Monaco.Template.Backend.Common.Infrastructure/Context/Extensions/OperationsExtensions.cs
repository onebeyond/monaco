using System.Linq.Expressions;
using LinqKit;
using Microsoft.EntityFrameworkCore;
using Monaco.Template.Common.Domain.Model;

namespace Monaco.Template.Common.Infrastructure.Context.Extensions;

public static class OperationsExtensions
{
	public static async Task<bool> ExistsAsync<T>(this DbContext dbContext, Guid id, CancellationToken cancellationToken = default) where T : Entity
	{
		return await dbContext.Set<T>().AsExpandable().AnyAsync(x => x.Id == id, cancellationToken);
	}

	public static async Task<bool> ExistsAsync<T>(this DbContext dbContext, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) where T : class
	{
		return await dbContext.Set<T>().AsExpandable().AnyAsync(predicate, cancellationToken);
	}

	public static async Task<T?> GetAsync<T>(this DbContext dbContext,
											 Guid? id,
											 CancellationToken cancellationToken) where T : class =>
		id.HasValue
			? await dbContext.GetAsync<T>(id.Value, cancellationToken)
			: null;

	public static async Task<T> GetAsync<T>(this DbContext dbContext,
											Guid id,
											CancellationToken cancellationToken) where T : class =>
		(await dbContext.Set<T>().FindAsync(new object?[] { id }, cancellationToken))!;

	public static IQueryable<TResult> Set<TResult>(this DbContext context, Type t) =>
		(IQueryable<TResult>)context.GetType()
									.GetMethod("Set", Type.EmptyTypes)?
									.MakeGenericMethod(t)
									.Invoke(context, Array.Empty<object?>())!;
}