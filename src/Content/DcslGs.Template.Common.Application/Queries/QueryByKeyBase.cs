using MediatR;

namespace DcslGs.Template.Common.Application.Queries;

public class QueryByKeyBase<T, TKey> : IRequest<T>
{
    public QueryByKeyBase(TKey key)
    {
        Key = key;
    }

    public TKey Key { get; }
}