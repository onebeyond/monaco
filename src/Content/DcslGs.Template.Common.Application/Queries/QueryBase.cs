using MediatR;
using Microsoft.Extensions.Primitives;

namespace DcslGs.Template.Common.Application.Queries;

public abstract class QueryBase<T> : IRequest<T>
{
    protected QueryBase(IEnumerable<KeyValuePair<string, StringValues>> queryString)
    {
        QueryString = queryString;
    }

    public virtual IEnumerable<KeyValuePair<string, StringValues>> QueryString { get; }
    public virtual string[] Sort => QueryString.FirstOrDefault(x => x.Key == "sort").Value.ToArray();

    protected bool Expand(string value) => QueryString.Any(x => x.Key.Equals("expand", StringComparison.InvariantCultureIgnoreCase) &&
                                                                x.Value.Contains(value, StringComparer.InvariantCultureIgnoreCase));
}