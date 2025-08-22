using Microsoft.Extensions.Primitives;
using Monaco.Template.Backend.Common.Domain.Model;

namespace Monaco.Template.Backend.Common.Application.Queries;

public abstract record QueryPagedBase<T>(IEnumerable<KeyValuePair<string, StringValues>> QueryParams) : QueryBase<Page<T>?>(QueryParams)
{
	public virtual int Offset => QueryParams.FirstOrDefault(x => x.Key.Equals(nameof(Page<T>.Pager.Offset), StringComparison.InvariantCultureIgnoreCase))
											.Value
											.Select(x => int.TryParse(x, out var y) ? y : 0)
											.Where(x => x >= 0)
											.DefaultIfEmpty(0)
											.FirstOrDefault();

	public virtual int Limit => QueryParams.FirstOrDefault(x => x.Key.Equals(nameof(Page<T>.Pager.Limit), StringComparison.InvariantCultureIgnoreCase))
										   .Value
										   .Select(x => int.TryParse(x, out var y) ? y : 0)
										   .Where(x => x is > 0 and <= 100)
										   .DefaultIfEmpty(10)
										   .FirstOrDefault();
}