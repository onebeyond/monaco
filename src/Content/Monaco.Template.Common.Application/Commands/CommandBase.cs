using MediatR;
using Monaco.Template.Common.Application.Commands.Contracts;

namespace Monaco.Template.Common.Application.Commands;

public abstract record CommandBase : IRequest<ICommandResult>
{
    protected CommandBase()
    {
    }

    protected CommandBase(Guid id)
    {
        Id = id;
    }

    public Guid Id { get; init; }
}

public abstract record CommandBase<TResult> : IRequest<ICommandResult<TResult>>
{
    protected CommandBase()
    {
    }

    protected CommandBase(Guid id)
    {
        Id = id;
    }

    public Guid Id { get; init; }
}