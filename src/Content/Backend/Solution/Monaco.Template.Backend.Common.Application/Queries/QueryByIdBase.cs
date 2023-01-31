using MediatR;

namespace Monaco.Template.Backend.Common.Application.Queries;

public abstract record QueryByIdBase<T>(Guid Id) : IRequest<T?>;