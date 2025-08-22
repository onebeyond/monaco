using MediatR;

namespace Monaco.Template.Backend.Common.Application.Queries;

/// <summary>
/// Represents a base query for retrieving an entity of type <typeparamref name="T"/> by its unique identifier.
/// </summary>
/// <remarks>This is an abstract record that implements the <see cref="IRequest{T}"/> interface, where
/// <typeparamref name="T"/>  represents the type of the result. Derived types should specify the entity type and may
/// include additional query parameters.</remarks>
/// <typeparam name="T">The type of the entity to be retrieved.</typeparam>
/// <param name="Id">The unique identifier of the entity to query.</param>
public abstract record QueryByIdBase<T>(Guid Id) : IRequest<T?>;