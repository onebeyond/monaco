using MediatR;

namespace Monaco.Template.Backend.Common.Application.Commands;

public abstract record CommandBase(Guid Id) : IRequest<CommandResult>
{
	protected CommandBase() : this(Guid.Empty)
	{
	}
}

public abstract record CommandBase<TResult>(Guid Id) : IRequest<CommandResult<TResult>>
{
	protected CommandBase() : this(Guid.Empty)
	{
	}
}