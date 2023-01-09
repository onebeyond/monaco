using MediatR;

namespace Monaco.Template.Common.Application.Queries;

public abstract record QueryByKeyBase<T, TKey>(TKey Key) : IRequest<T>;