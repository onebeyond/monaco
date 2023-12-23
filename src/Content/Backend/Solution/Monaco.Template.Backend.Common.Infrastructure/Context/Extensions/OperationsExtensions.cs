using Microsoft.EntityFrameworkCore;
using Monaco.Template.Backend.Common.Domain.Model;
using System.Linq.Expressions;

namespace Monaco.Template.Backend.Common.Infrastructure.Context.Extensions;

public static class OperationsExtensions
{
	public static Task<bool> ExistsAsync<T>(this DbContext dbContext,
											Guid id,
											CancellationToken cancellationToken) where T : Entity =>
		dbContext.Set<T>().AnyAsync(x => x.Id == id, cancellationToken);

	public static Task<bool> ExistsAsync<T>(this DbContext dbContext,
											Expression<Func<T, bool>> predicate,
											CancellationToken cancellationToken) where T : class =>
		dbContext.Set<T>().AnyAsync(predicate, cancellationToken);

	public static async Task<T?> GetAsync<T>(this DbContext dbContext,
											 Guid? id,
											 CancellationToken cancellationToken) where T : class =>
		id.HasValue
			? await dbContext.GetAsync<T>(id.Value, cancellationToken)
			: null;

	public static async Task<T> GetAsync<T>(this DbContext dbContext,
											Guid id,
											CancellationToken cancellationToken) where T : class =>
		(await dbContext.Set<T>().FindAsync([id], cancellationToken))!;

	public static IQueryable<TResult> Set<TResult>(this DbContext context, Type t) =>
		(IQueryable<TResult>)context.GetType()
									.GetMethod("Set", Type.EmptyTypes)?
									.MakeGenericMethod(t)
									.Invoke(context, [])!;

	public static async Task<List<T>> GetListByIdsAsync<T>(this DbContext dbContext,
														   Guid[] items,
														   CancellationToken cancellationToken) where T : Entity =>
		items.Any()
			? await dbContext.Set<T>()
							 .Where(x => items.Contains(x.Id))
							 .ToListAsync(cancellationToken)
			: [];
}