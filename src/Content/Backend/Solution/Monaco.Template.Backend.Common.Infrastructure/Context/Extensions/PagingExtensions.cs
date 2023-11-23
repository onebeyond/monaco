using DelegateDecompiler.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Monaco.Template.Backend.Common.Domain.Model;
using System.Linq.Expressions;

namespace Monaco.Template.Backend.Common.Infrastructure.Context.Extensions;

public static class PagingExtensions
{
	public static async Task<Page<TResult>> ToPageAsync<T, TResult>(this IQueryable<T> query,
																	int offset,
																	int limit,
																	Func<T, TResult> selector,
																	CancellationToken cancellationToken = default)
	{
		var totalItems = await query.CountAsync(cancellationToken);
		var list = await query.Skip(offset)
							  .Take(limit)
							  .ToListAsync(cancellationToken);
		return new(list.Select(selector),
				   offset,
				   limit,
				   totalItems);
	}

	public static async Task<Page<TResult>> ToPageComputedAsync<T, TResult>(this IQueryable<T> query,
																			int offset,
																			int limit,
																			Expression<Func<T, TResult>> selector,
																			CancellationToken cancellationToken = default)
	{
		var totalItems = await query.CountAsync(cancellationToken);
		var list = await query.Skip(offset)
							  .Take(limit)
							  .Select(selector)
							  .DecompileAsync()
							  .ToListAsync(cancellationToken);
		return new(list,
				   offset,
				   limit,
				   totalItems);
	}

	public static async Task<Page<TResult>> ToPageAsync<T, TKey, TResult>(this IQueryable<T> query,
																		  int offset,
																		  int limit,
																		  Func<T, TResult> selector,
																		  Expression<Func<T, TKey>>? orderBy,
																		  CancellationToken cancellationToken = default)
	{
		var count = await query.CountAsync(cancellationToken);
		if (orderBy != null)
			query = query.OrderBy(orderBy);
		var list = await query.Skip(offset)
							  .Take(limit)
							  .ToListAsync(cancellationToken);
		return new(list.Select(selector),
				   offset,
				   limit,
				   count);
	}

	public static async Task<Page<TResult>> ToPageAsync<T, TKey, TSec, TResult>(this IQueryable<T> query,
																				int offset,
																				int limit,
																				Func<T, TResult> selector,
																				Expression<Func<T, TKey>>? orderBy,
																				Expression<Func<T, TSec>>? thenBy,
																				CancellationToken cancellationToken = default)
	{
		var totalItems = await query.CountAsync(cancellationToken);
		if (orderBy != null)
			query = thenBy != null 
						? query.OrderBy(orderBy)
							   .ThenBy(thenBy)
						: query.OrderBy(orderBy);
		var list = await query.Skip(offset)
							  .Take(limit)
							  .ToListAsync(cancellationToken);
		return new(list.Select(selector),
				   offset,
				   limit,
				   totalItems);
	}
	
	public static Page<TResult> ToPage<T, TResult>(this IEnumerable<T> enumerable, int offset, int limit, Func<T, TResult> selector)
	{
		var list = enumerable.ToList();
		return new(list.Skip(offset)
					   .Take(limit)
					   .Select(selector),
				   offset,
				   limit,
				   list.Count);
	}
}