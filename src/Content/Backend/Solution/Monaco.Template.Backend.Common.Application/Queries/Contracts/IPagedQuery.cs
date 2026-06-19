using Microsoft.Extensions.Primitives;
using Monaco.Template.Backend.Common.Domain.Model;

namespace Monaco.Template.Backend.Common.Application.Queries.Contracts;

public interface IPagedQuery
{
	const int MaxLimit = 100;

	IEnumerable<KeyValuePair<string, StringValues>> QueryParams { get; }

	static int ParseOffset(IEnumerable<KeyValuePair<string, StringValues>> queryParams) =>
		queryParams.FirstOrDefault(x => x.Key.Equals(nameof(Page<>.Pager.Offset), StringComparison.InvariantCultureIgnoreCase))
				   .Value
				   .Select(x => int.TryParse(x, out var y) ? y : 0)
				   .Where(x => x >= 0)
				   .DefaultIfEmpty(0)
				   .FirstOrDefault();

	static int ParseLimit(IEnumerable<KeyValuePair<string, StringValues>> queryParams) =>
		queryParams.FirstOrDefault(x => x.Key.Equals(nameof(Page<>.Pager.Limit), StringComparison.InvariantCultureIgnoreCase))
				   .Value
				   .Select(x => int.TryParse(x, out var y) ? y : 0)
				   .Where(x => x is > 0 and <= MaxLimit)
				   .DefaultIfEmpty(10)
				   .FirstOrDefault();
}