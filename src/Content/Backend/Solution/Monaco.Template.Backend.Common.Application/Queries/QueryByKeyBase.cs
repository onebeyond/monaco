using MediatR;

namespace Monaco.Template.Backend.Common.Application.Queries;

public abstract record QueryByKeyBase<T, TKey>(TKey Key) : IRequest<T>;