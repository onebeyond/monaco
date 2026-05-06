using Microsoft.EntityFrameworkCore;
using Monaco.Template.Backend.Common.Domain.Model;
using Monaco.Template.Backend.Common.Infrastructure.Context;
using Monaco.Template.Backend.Common.Infrastructure.Context.Extensions;
using System.Linq.Expressions;

namespace Monaco.Template.Backend.Common.Application.Queries.Extensions;

public static class QueryExtensions
{
	extension<TEntity, TResult>(QueryBase<List<TResult>, TEntity> request) where TEntity : Entity
	{
		public async Task<QueryResult<List<TResult>>> ExecuteQueryAsync(BaseDbContext dbContext,
																		Func<TEntity, TResult> selector,
																		string defaultSortField,
																		Dictionary<string, Expression<Func<TEntity, object>>> mappedFieldsFilter,
																		Dictionary<string, Expression<Func<TEntity, object>>> mappedFieldsSort,
																		CancellationToken cancellationToken)
		{
			var result = await dbContext.Set<TEntity>()
										.AsNoTracking()
										.ApplyFilter(request.QueryParams, mappedFieldsFilter)
										.ApplySort(request.Sort, defaultSortField, mappedFieldsSort)
										.ToListAsync(cancellationToken);
			return QueryResult<List<TResult>>.Success([..result.Select(selector)]);
		}

		public Task<QueryResult<List<TResult>>> ExecuteQueryAsync(BaseDbContext dbContext,
																  Func<TEntity, TResult> selector,
																  string defaultSortField,
																  Dictionary<string, Expression<Func<TEntity, object>>> mappedFieldsFilter,
																  CancellationToken cancellationToken) =>
			request.ExecuteQueryAsync(dbContext,
									  selector,
									  defaultSortField,
									  mappedFieldsFilter,
									  mappedFieldsFilter,
									  cancellationToken);

		public async Task<QueryResult<List<TResult>>> ExecuteQueryAsync(BaseDbContext dbContext,
																		Func<TEntity, TResult> selector,
																		Func<IQueryable<TEntity>, IQueryable<TEntity>> queryFunc,
																		string defaultSortField,
																		Dictionary<string, Expression<Func<TEntity, object>>> mappedFieldsFilter,
																		Dictionary<string, Expression<Func<TEntity, object>>> mappedFieldsSort,
																		CancellationToken cancellationToken)
		{
			var query = dbContext.Set<TEntity>().AsQueryable();
			query = queryFunc.Invoke(query);

			var result = await query.ApplyFilter(request.QueryParams, mappedFieldsFilter)
									.ApplySort(request.Sort, defaultSortField, mappedFieldsSort)
									.ToListAsync(cancellationToken);
			return QueryResult<List<TResult>>.Success([.. result.Select(selector)]);
		}

		public Task<QueryResult<List<TResult>>> ExecuteQueryAsync(BaseDbContext dbContext,
																  Func<TEntity, TResult> selector,
																  Func<IQueryable<TEntity>, IQueryable<TEntity>> queryFunc,
																  string defaultSortField,
																  Dictionary<string, Expression<Func<TEntity, object>>> mappedFieldsFilter,
																  CancellationToken cancellationToken) =>
			request.ExecuteQueryAsync(dbContext,
									  selector,
									  queryFunc,
									  defaultSortField,
									  mappedFieldsFilter,
									  mappedFieldsFilter,
									  cancellationToken);

		public async Task<QueryResult<List<TResult>>> ExecuteQueryAsync(BaseDbContext dbContext,
																		Func<TEntity, TResult> selector,
																		Func<QueryBase<List<TResult>, TEntity>, Expression<Func<TEntity, bool>>> expression,
																		string defaultSortField,
																		Dictionary<string, Expression<Func<TEntity, object>>> mappedFieldsFilter,
																		Dictionary<string, Expression<Func<TEntity, object>>> mappedFieldsSort,
																		CancellationToken cancellationToken)
		{
			var result = await dbContext.Set<TEntity>()
										.AsNoTracking()
										.Where(expression.Invoke(request))
										.ApplyFilter(request.QueryParams, mappedFieldsFilter)
										.ApplySort(request.Sort, defaultSortField, mappedFieldsSort)
										.ToListAsync(cancellationToken);
			return QueryResult<List<TResult>>.Success([.. result.Select(selector)]);
		}

		public Task<QueryResult<List<TResult>>> ExecuteQueryAsync(BaseDbContext dbContext,
																  Func<TEntity, TResult> selector,
																  Func<QueryBase<List<TResult>, TEntity>, Expression<Func<TEntity, bool>>> expression,
																  string defaultSortField,
																  Dictionary<string, Expression<Func<TEntity, object>>> mappedFieldsFilter,
																  CancellationToken cancellationToken) =>
			request.ExecuteQueryAsync(dbContext,
									  selector,
									  expression,
									  defaultSortField,
									  mappedFieldsFilter,
									  mappedFieldsFilter,
									  cancellationToken);
	}

	extension<TReq, TEntity, TResult>(TReq request) where TReq : QueryBase<List<TResult>, TEntity> where TEntity : Entity
	{
		public async Task<QueryResult<List<TResult>>> ExecuteQueryAsync(BaseDbContext dbContext,
																		Func<TEntity, TResult> selector,
																		Func<TReq, Expression<Func<TEntity, bool>>> expression,
																		string defaultSortField,
																		Dictionary<string, Expression<Func<TEntity, object>>> mappedFieldsFilter,
																		Dictionary<string, Expression<Func<TEntity, object>>> mappedFieldsSort,
																		CancellationToken cancellationToken)
		{
			var result = await dbContext.Set<TEntity>()
										.AsNoTracking()
										.Where(expression.Invoke(request))
										.ApplyFilter(request.QueryParams, mappedFieldsFilter)
										.ApplySort(request.Sort, defaultSortField, mappedFieldsSort)
										.ToListAsync(cancellationToken);
			return QueryResult<List<TResult>>.Success([..result.Select(selector)]);
		}

		public Task<QueryResult<List<TResult>>> ExecuteQueryAsync(BaseDbContext dbContext,
																  Func<TEntity, TResult> selector,
																  Func<TReq, Expression<Func<TEntity, bool>>> expression,
																  string defaultSortField,
																  Dictionary<string, Expression<Func<TEntity, object>>> mappedFieldsFilter,
																  CancellationToken cancellationToken) =>
			request.ExecuteQueryAsync(dbContext,
									  selector,
									  expression,
									  defaultSortField,
									  mappedFieldsFilter,
									  mappedFieldsFilter,
									  cancellationToken);
	}

	extension<TEntity, TResult>(QueryPagedBase<TResult> request) where TEntity : Entity
	{
		public async Task<QueryResult<Page<TResult>>> ExecuteQueryAsync(BaseDbContext dbContext,
																		Func<TEntity, TResult> selector,
																		string defaultSortField,
																		Dictionary<string, Expression<Func<TEntity, object>>> mappedFieldsFilter,
																		Dictionary<string, Expression<Func<TEntity, object>>> mappedFieldsSort,
																		CancellationToken cancellationToken) =>
			QueryResult<Page<TResult>>.Success(await dbContext.Set<TEntity>()
															  .AsNoTracking()
															  .ApplyFilter(request.QueryParams, mappedFieldsFilter)
															  .ApplySort(request.Sort, defaultSortField, mappedFieldsSort)
															  .ToPageAsync(request.Offset, request.Limit, selector, cancellationToken));

		public Task<QueryResult<Page<TResult>>> ExecuteQueryAsync(BaseDbContext dbContext,
																  Func<TEntity, TResult> selector,
																  string defaultSortField,
																  Dictionary<string, Expression<Func<TEntity, object>>> mappedFieldsFilter,
																  CancellationToken cancellationToken) =>
			request.ExecuteQueryAsync(dbContext,
									  selector,
									  defaultSortField,
									  mappedFieldsFilter,
									  mappedFieldsFilter,
									  cancellationToken);

		public async Task<QueryResult<Page<TResult>>> ExecuteQueryAsync(BaseDbContext dbContext,
																		Func<TEntity, TResult> selector,
																		Func<QueryPagedBase<TResult>, Expression<Func<TEntity, bool>>> expression,
																		string defaultSortField,
																		Dictionary<string, Expression<Func<TEntity, object>>> mappedFieldsFilter,
																		Dictionary<string, Expression<Func<TEntity, object>>> mappedFieldsSort,
																		CancellationToken cancellationToken) =>
			QueryResult<Page<TResult>>.Success(await dbContext.Set<TEntity>()
															  .AsNoTracking()
															  .Where(expression.Invoke(request))
															  .ApplyFilter(request.QueryParams, mappedFieldsFilter)
															  .ApplySort(request.Sort, defaultSortField, mappedFieldsSort)
															  .ToPageAsync(request.Offset, request.Limit, selector, cancellationToken));

		public async Task<QueryResult<Page<TResult>>> ExecuteQueryAsync(BaseDbContext dbContext,
																		Func<TEntity, TResult> selector,
																		Func<QueryPagedBase<TResult>, Expression<Func<TEntity, bool>>> expression,
																		string defaultSortField,
																		Dictionary<string, Expression<Func<TEntity, object>>> mappedFieldsFilter,
																		CancellationToken cancellationToken) =>
			await request.ExecuteQueryAsync(dbContext,
											selector,
											expression,
											defaultSortField,
											mappedFieldsFilter,
											mappedFieldsFilter,
											cancellationToken);
	}

	extension<TEntity, TResult>(QueryByIdBase<QueryResult<TResult>> request) where TEntity : Entity
	{
		public async Task<QueryResult<TResult>> ExecuteQueryAsync(BaseDbContext dbContext,
																  Func<TEntity?, TResult?> selector,
																  CancellationToken cancellationToken)
		{
			var item = await dbContext.Set<TEntity>()
									  .AsNoTracking()
									  .SingleOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
			return QueryResult<TResult>.SuccessOrNotFound(selector.Invoke(item));
		}
	}

	extension<TReq, TEntity, TResult>(TReq request) where TReq : QueryByIdBase<QueryResult<TResult>> where TEntity : Entity
	{
		public async Task<QueryResult<TResult>> ExecuteQueryAsync(BaseDbContext dbContext,
																  Func<TEntity?, TResult?> selector,
																  Func<TReq, Expression<Func<TEntity, bool>>> expression,
																  CancellationToken cancellationToken)
		{
			var item = await dbContext.Set<TEntity>()
									  .AsNoTracking()
									  .Where(expression.Invoke(request))
									  .SingleOrDefaultAsync(cancellationToken);
			return QueryResult<TResult>.SuccessOrNotFound(selector.Invoke(item));
		}
	}
}