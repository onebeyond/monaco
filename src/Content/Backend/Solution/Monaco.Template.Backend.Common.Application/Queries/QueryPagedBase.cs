using Microsoft.Extensions.Primitives;
using Monaco.Template.Backend.Common.Application.Queries.Contracts;
using Monaco.Template.Backend.Common.Domain.Model;

namespace Monaco.Template.Backend.Common.Application.Queries;

public abstract record QueryPagedBase<TResult>(IEnumerable<KeyValuePair<string, StringValues>> QueryParams) 
	: QueryBase<QueryResult<Page<TResult>>>(QueryParams), IPagedQuery
{
	public int Offset => IPagedQuery.ParseOffset(QueryParams);
	public int Limit => IPagedQuery.ParseLimit(QueryParams);
}

public abstract record QueryPagedBase<TResult, TEntity>(IEnumerable<KeyValuePair<string, StringValues>> QueryParams)
	: QueryBase<Page<TResult>, TEntity>(QueryParams), IPagedQuery where TEntity : class
{
	public int Offset => IPagedQuery.ParseOffset(QueryParams);
	public int Limit => IPagedQuery.ParseLimit(QueryParams);
}