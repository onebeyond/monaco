using MediatR;

namespace Monaco.Template.Backend.Common.Application.Queries;

/// <summary>
/// Represents a base class for queries that retrieve a single result of type <typeparamref name="T"/>  based on a
/// unique key of type <typeparamref name="TKey"/>.
/// </summary>
/// <remarks> It enforces a consistent pattern for implementing queries
/// that require a key-based lookup, improving code readability and maintainability</remarks>
/// <typeparam name="T">The type of the result returned by the query.</typeparam>
/// <typeparam name="TKey">The type of the key used to identify the result.</typeparam>
/// <param name="Key">The unique key used to identify the result of the query. Cannot be null.</param>
public abstract record QueryByKeyBase<T, TKey>(TKey Key) : IRequest<T>;