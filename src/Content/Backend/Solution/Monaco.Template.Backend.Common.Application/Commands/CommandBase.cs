using MediatR;

namespace Monaco.Template.Backend.Common.Application.Commands;

/// <summary>
/// Represents the base class for all command types in the application.
/// </summary>
/// <remarks>This abstract record serves as a foundation for defining commands that implement the <see
/// cref="IRequest{TResponse}"/> interface, where the response type is <see cref="CommandResult"/>. Each command is
/// uniquely identified by a <see cref="Guid"/>.</remarks>
/// <param name="Id"></param>
public abstract record CommandBase(Guid Id) : IRequest<CommandResult>
{
	protected CommandBase() : this(Guid.Empty)
	{
	}
}

/// <summary>
/// Represents the base class for command objects that encapsulate a request and its associated result type.
/// </summary>
/// <remarks>This class serves as a foundation for creating command objects that implement the <see
/// cref="IRequest{TResponse}"/> interface, where the response is a <see cref="CommandResult{TResult}"/>. Each command
/// is uniquely identified by a <see cref="Guid"/>.</remarks>
/// <typeparam name="TResult">The type of the result produced by the command.</typeparam>
/// <param name="Id"></param>
public abstract record CommandBase<TResult>(Guid Id) : IRequest<CommandResult<TResult>>
{
	protected CommandBase() : this(Guid.Empty)
	{
	}
}