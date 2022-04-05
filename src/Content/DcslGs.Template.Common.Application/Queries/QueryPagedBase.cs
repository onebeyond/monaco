using Microsoft.Extensions.Primitives;
using DcslGs.Template.Common.Domain.Model;

namespace DcslGs.Template.Common.Application.Queries;

public abstract class QueryPagedBase<T> : QueryBase<Page<T>?>
{
    protected QueryPagedBase(IEnumerable<KeyValuePair<string, StringValues>> queryString) : base(queryString)
    {
    }

    public virtual int Offset => QueryString.FirstOrDefault(x => x.Key == "offset")
                                            .Value
                                            .Select(x => int.TryParse(x, out var y) ? y : 0)
                                            .Where(x => x >= 0)
                                            .DefaultIfEmpty(0)
                                            .FirstOrDefault();

    public virtual int Limit => QueryString.FirstOrDefault(x => x.Key == "limit")
                                           .Value
                                           .Select(x => int.TryParse(x, out var y) ? y : 0)
                                           .Where(x => x is > 0 and <= 100)
                                           .DefaultIfEmpty(10)
                                           .FirstOrDefault();
}