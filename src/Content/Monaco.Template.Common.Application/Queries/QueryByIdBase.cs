using MediatR;

namespace Monaco.Template.Common.Application.Queries;

public abstract record QueryByIdBase<T> : IRequest<T?>
{
    protected QueryByIdBase(Guid id)
    {
        Id = id;
    }

    public Guid Id { get; }
}