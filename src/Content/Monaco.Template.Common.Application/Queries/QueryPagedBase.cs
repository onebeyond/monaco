using Microsoft.Extensions.Primitives;
using Monaco.Template.Common.Domain.Model;

namespace Monaco.Template.Common.Application.Queries;

public abstract record QueryPagedBase<T> : QueryBase<Page<T>?>
{
    protected QueryPagedBase(IEnumerable<KeyValuePair<string, StringValues>> queryString) : base(queryString)
    {
    }

    public virtual int Offset => QueryString.FirstOrDefault(x => x.Key.Equals(nameof(Page<T>.Pager.Offset), StringComparison.InvariantCultureIgnoreCase))
                                            .Value
                                            .Select(x => int.TryParse(x, out var y) ? y : 0)
                                            .Where(x => x >= 0)
                                            .DefaultIfEmpty(0)
                                            .FirstOrDefault();

    public virtual int Limit => QueryString.FirstOrDefault(x => x.Key.Equals(nameof(Page<T>.Pager.Limit), StringComparison.InvariantCultureIgnoreCase))
                                           .Value
                                           .Select(x => int.TryParse(x, out var y) ? y : 0)
                                           .Where(x => x is > 0 and <= 100)
                                           .DefaultIfEmpty(10)
                                           .FirstOrDefault();
}