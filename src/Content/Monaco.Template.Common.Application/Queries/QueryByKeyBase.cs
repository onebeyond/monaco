using MediatR;

namespace Monaco.Template.Common.Application.Queries;

public abstract record QueryByKeyBase<T, TKey> : IRequest<T>
{
	protected QueryByKeyBase(TKey key)
    {
        Key = key;
    }

    public TKey Key { get; }
}