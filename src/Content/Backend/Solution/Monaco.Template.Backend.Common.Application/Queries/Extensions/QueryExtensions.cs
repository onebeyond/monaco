using Microsoft.EntityFrameworkCore;
using Monaco.Template.Backend.Common.Domain.Model;
using Monaco.Template.Backend.Common.Infrastructure.Context;
using Monaco.Template.Backend.Common.Infrastructure.Context.Extensions;
using System.Linq.Expressions;

namespace Monaco.Template.Backend.Common.Application.Queries.Extensions;

public static class QueryExtensions
{
	public static async Task<List<TResult>> ExecuteQueryAsync<T, TResult>(this QueryBase<List<TResult>> request,
																		  BaseDbContext dbContext,
																		  Func<T, TResult> selector,
																		  string defaultSortField,
																		  Dictionary<string, Expression<Func<T, object>>> mappedFieldsFilter,
																		  Dictionary<string, Expression<Func<T, object>>> mappedFieldsSort,
																		  CancellationToken cancellationToken) where T : Entity
	{
		var result = await dbContext.Set<T>()
									.AsNoTracking()
									.ApplyFilter(request.QueryParams, mappedFieldsFilter)
									.ApplySort(request.Sort, defaultSortField, mappedFieldsSort)
									.ToListAsync(cancellationToken);
		return result.Select(selector)
					 .ToList();
	}

	public static Task<List<TResult>> ExecuteQueryAsync<T, TResult>(this QueryBase<List<TResult>> request,
																	BaseDbContext dbContext,
																	Func<T, TResult> selector,
																	string defaultSortField,
																	Dictionary<string, Expression<Func<T, object>>> mappedFieldsFilter,
																	CancellationToken cancellationToken) where T : Entity =>
		request.ExecuteQueryAsync(dbContext,
								  selector,
								  defaultSortField,
								  mappedFieldsFilter,
								  mappedFieldsFilter,
								  cancellationToken);

	public static async Task<List<TResult>> ExecuteQueryAsync<T, TResult>(this QueryBase<List<TResult>> request,
																		  BaseDbContext dbContext,
																		  Func<T, TResult> selector,
																		  Func<IQueryable<T>, IQueryable<T>> queryFunc,
																		  string defaultSortField,
																		  Dictionary<string, Expression<Func<T, object>>> mappedFieldsFilter,
																		  Dictionary<string, Expression<Func<T, object>>> mappedFieldsSort,
																		  CancellationToken cancellationToken) where T : Entity
	{
		var query = dbContext.Set<T>().AsQueryable();
		query = queryFunc.Invoke(query);

		var result = await query.ApplyFilter(request.QueryParams, mappedFieldsFilter)
								.ApplySort(request.Sort, defaultSortField, mappedFieldsSort)
								.ToListAsync(cancellationToken);
		return result.Select(selector).ToList();
	}

	public static Task<List<TResult>> ExecuteQueryAsync<T, TResult>(this QueryBase<List<TResult>> request,
																	BaseDbContext dbContext,
																	Func<T, TResult> selector,
																	Func<IQueryable<T>, IQueryable<T>> queryFunc,
																	string defaultSortField,
																	Dictionary<string, Expression<Func<T, object>>> mappedFieldsFilter,
																	CancellationToken cancellationToken) where T : Entity =>
		request.ExecuteQueryAsync(dbContext,
								  selector,
								  queryFunc,
								  defaultSortField,
								  mappedFieldsFilter,
								  mappedFieldsFilter,
								  cancellationToken);

	public static async Task<List<TResult>> ExecuteQueryAsync<TReq, T, TResult>(this TReq request,
																				BaseDbContext dbContext,
																				Func<T, TResult> selector,
																				Func<TReq, Expression<Func<T, bool>>> expression,
																				string defaultSortField,
																				Dictionary<string, Expression<Func<T, object>>> mappedFieldsFilter,
																				Dictionary<string, Expression<Func<T, object>>> mappedFieldsSort,
																				CancellationToken cancellationToken) where TReq : QueryBase<List<TResult>>
																													 where T : Entity
	{
		var result = await dbContext.Set<T>()
									.AsNoTracking()
									.Where(expression.Invoke(request))
									.ApplyFilter(request.QueryParams, mappedFieldsFilter)
									.ApplySort(request.Sort, defaultSortField, mappedFieldsSort)
									.ToListAsync(cancellationToken);
		return result.Select(selector)
					 .ToList();
	}

	public static Task<List<TResult>> ExecuteQueryAsync<TReq, T, TResult>(this TReq request,
																		  BaseDbContext dbContext,
																		  Func<T, TResult> selector,
																		  Func<TReq, Expression<Func<T, bool>>> expression,
																		  string defaultSortField,
																		  Dictionary<string, Expression<Func<T, object>>> mappedFieldsFilter,
																		  CancellationToken cancellationToken) where TReq : QueryBase<List<TResult>>
																											   where T : Entity =>
		request.ExecuteQueryAsync(dbContext,
								  selector,
								  expression,
								  defaultSortField,
								  mappedFieldsFilter,
								  mappedFieldsFilter,
								  cancellationToken);

	public static async Task<List<TResult>> ExecuteQueryAsync<T, TResult>(this QueryBase<List<TResult>> request,
																		  BaseDbContext dbContext,
																		  Func<T, TResult> selector,
																		  Func<QueryBase<List<TResult>>, Expression<Func<T, bool>>> expression,
																		  string defaultSortField,
																		  Dictionary<string, Expression<Func<T, object>>> mappedFieldsFilter,
																		  Dictionary<string, Expression<Func<T, object>>> mappedFieldsSort,
																		  CancellationToken cancellationToken) where T : Entity
	{
		var result = await dbContext.Set<T>()
									.AsNoTracking()
									.Where(expression.Invoke(request))
									.ApplyFilter(request.QueryParams, mappedFieldsFilter)
									.ApplySort(request.Sort, defaultSortField, mappedFieldsSort)
									.ToListAsync(cancellationToken);
		return result.Select(selector).ToList();
	}

	public static Task<List<TResult>> ExecuteQueryAsync<T, TResult>(this QueryBase<List<TResult>> request,
																	BaseDbContext dbContext,
																	Func<T, TResult> selector,
																	Func<QueryBase<List<TResult>>, Expression<Func<T, bool>>> expression,
																	string defaultSortField,
																	Dictionary<string, Expression<Func<T, object>>> mappedFieldsFilter,
																	CancellationToken cancellationToken) where T : Entity =>
		request.ExecuteQueryAsync(dbContext,
								  selector,
								  expression,
								  defaultSortField,
								  mappedFieldsFilter,
								  mappedFieldsFilter,
								  cancellationToken);

	public static Task<Page<TResult>> ExecuteQueryAsync<T, TResult>(this QueryPagedBase<TResult> request,
																	BaseDbContext dbContext,
																	Func<T, TResult> selector,
																	string defaultSortField,
																	Dictionary<string, Expression<Func<T, object>>> mappedFieldsFilter,
																	Dictionary<string, Expression<Func<T, object>>> mappedFieldsSort,
																	CancellationToken cancellationToken) where T : Entity =>
		dbContext.Set<T>()
				 .AsNoTracking()
				 .ApplyFilter(request.QueryParams, mappedFieldsFilter)
				 .ApplySort(request.Sort, defaultSortField, mappedFieldsSort)
				 .ToPageAsync(request.Offset, request.Limit, selector, cancellationToken);

	public static Task<Page<TResult>> ExecuteQueryAsync<T, TResult>(this QueryPagedBase<TResult> request,
																	BaseDbContext dbContext,
																	Func<T, TResult> selector,
																	string defaultSortField,
																	Dictionary<string, Expression<Func<T, object>>> mappedFieldsFilter,
																	CancellationToken cancellationToken) where T : Entity =>
		request.ExecuteQueryAsync(dbContext,
								  selector,
								  defaultSortField,
								  mappedFieldsFilter,
								  mappedFieldsFilter,
								  cancellationToken);

	public static Task<Page<TResult>> ExecuteQueryAsync<T, TResult>(this QueryPagedBase<TResult> request,
																	BaseDbContext dbContext,
																	Func<T, TResult> selector,
																	Func<QueryPagedBase<TResult>, Expression<Func<T, bool>>> expression,
																	string defaultSortField,
																	Dictionary<string, Expression<Func<T, object>>> mappedFieldsFilter,
																	Dictionary<string, Expression<Func<T, object>>> mappedFieldsSort,
																	CancellationToken cancellationToken) where T : Entity =>
		dbContext.Set<T>()
				 .AsNoTracking()
				 .Where(expression.Invoke(request))
				 .ApplyFilter(request.QueryParams, mappedFieldsFilter)
				 .ApplySort(request.Sort, defaultSortField, mappedFieldsSort)
				 .ToPageAsync(request.Offset, request.Limit, selector, cancellationToken);

	public static Task<Page<TResult>> ExecuteQueryAsync<T, TResult>(this QueryPagedBase<TResult> request,
																	BaseDbContext dbContext,
																	Func<T, TResult> selector,
																	Func<QueryPagedBase<TResult>, Expression<Func<T, bool>>> expression,
																	string defaultSortField,
																	Dictionary<string, Expression<Func<T, object>>> mappedFieldsFilter,
																	CancellationToken cancellationToken) where T : Entity =>
		request.ExecuteQueryAsync(dbContext,
								  selector,
								  expression,
								  defaultSortField,
								  mappedFieldsFilter,
								  mappedFieldsFilter,
								  cancellationToken);

	public static async Task<TResult?> ExecuteQueryAsync<T, TResult>(this QueryByIdBase<TResult?> request,
																	 BaseDbContext dbContext,
																	 Func<T?, TResult?> selector,
																	 CancellationToken cancellationToken) where T : Entity
	{
		var item = await dbContext.Set<T>()
								  .AsNoTracking()
								  .SingleOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
		return selector.Invoke(item);
	}

	public static async Task<TResult?> ExecuteQueryAsync<TReq, T, TResult>(this TReq request,
																		   BaseDbContext dbContext,
																		   Func<T?, TResult?> selector,
																		   Func<TReq, Expression<Func<T, bool>>> expression,
																		   CancellationToken cancellationToken) where TReq : QueryByIdBase<TResult>
																												where T : Entity
	{
		var item = await dbContext.Set<T>()
								  .AsNoTracking()
								  .Where(expression.Invoke(request))
								  .SingleOrDefaultAsync(cancellationToken);
		return selector.Invoke(item);
	}
}