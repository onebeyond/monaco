using MediatR;
using Monaco.Template.Common.Application.Commands.Contracts;

namespace Monaco.Template.Common.Application.Commands;

public abstract record CommandBase(Guid Id) : IRequest<ICommandResult>
{
    protected CommandBase() : this(Guid.Empty)
    {
    }
}

public abstract record CommandBase<TResult>(Guid Id) : IRequest<ICommandResult<TResult>>
{
	protected CommandBase() : this(Guid.Empty)
	{
	}
}