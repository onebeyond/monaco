using MediatR;
using Monaco.Template.Backend.Common.Application.Commands.Contracts;

namespace Monaco.Template.Backend.Common.Application.Commands;

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