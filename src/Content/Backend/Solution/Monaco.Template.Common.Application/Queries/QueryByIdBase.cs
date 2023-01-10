using MediatR;

namespace Monaco.Template.Common.Application.Queries;

public abstract record QueryByIdBase<T>(Guid Id) : IRequest<T?>;