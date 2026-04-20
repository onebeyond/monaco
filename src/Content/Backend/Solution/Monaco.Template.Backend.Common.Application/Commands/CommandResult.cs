using FluentValidation.Results;

namespace Monaco.Template.Backend.Common.Application.Commands;

/// <summary>
/// Represents the result of a command execution.
/// </summary>
/// <remarks>This type encapsulates the outcome of a command operation and acts as a discriminated union whose
/// concrete variants are <see cref="Success{T}"/>, <see cref="NotFound{T}"/>, <see cref="ValidationFailure{T}"/>,
/// <see cref="ConcurrencyConflict{T}"/> and <see cref="Forbidden{T}"/>. Use the static factory methods to create
/// instances representing each possible outcome.</remarks>
/// <typeparam name="T">The type of the result value returned by the command.</typeparam>
public abstract record CommandResult<T> : CommandResult
{
    /// <summary>
    /// Creates a <see cref="CommandResult{T}"/> instance representing a successful operation.
    /// </summary>
    /// <param name="result">The result value to associate with the successful command.</param>
    /// <returns>A <see cref="Success{T}"/> instance representing a successful command execution.</returns>
    public static Success<T> Success(T result) =>
        new(result);

    /// <summary>
    /// Creates a <see cref="CommandResult{T}"/> instance representing a "Not Found" result.
    /// </summary>
    /// <returns>A <see cref="NotFound{T}"/> instance indicating that the target item was not found.</returns>
    public new static NotFound<T> NotFound() =>
        new();

    /// <summary>
    /// Creates a <see cref="CommandResult{T}"/> instance representing a failed validation.
    /// </summary>
    /// <param name="validationResult">The result of the validation process, containing details about the validation errors.</param>
    /// <returns>A <see cref="ValidationFailure{T}"/> instance carrying the provided <paramref name="validationResult"/>.</returns>
    public new static ValidationFailure<T> ValidationFailure(ValidationResult validationResult) =>
        new(validationResult);

    /// <summary>
    /// Creates a <see cref="CommandResult{T}"/> instance representing a concurrency conflict.
    /// </summary>
    /// <returns>A <see cref="ConcurrencyConflict{T}"/> instance indicating that a concurrency conflict was encountered while executing the command.</returns>
    public new static ConcurrencyConflict<T> ConcurrencyConflict() =>
        new();

    /// <summary>
    /// Creates a <see cref="CommandResult{T}"/> instance representing a forbidden operation.
    /// </summary>
    /// <returns>A <see cref="Forbidden{T}"/> instance indicating that the command execution is not allowed.</returns>
    public new static Forbidden<T> Forbidden() =>
        new();
}

/// <summary>
/// Represents the result of a command execution.
/// </summary>
/// <remarks>This type encapsulates the outcome of a command operation and acts as a discriminated union whose
/// concrete variants are <see cref="Success"/>, <see cref="Commands.NotFound"/>,
/// <see cref="Commands.ValidationFailure"/>, <see cref="Commands.ConcurrencyConflict"/> and
/// <see cref="Commands.Forbidden"/>. Use the static factory methods to create instances representing each possible
/// outcome.</remarks>
public abstract record CommandResult
{
    /// <summary>
    /// Creates a <see cref="CommandResult"/> instance representing a successful operation.
    /// </summary>
    /// <returns>A <see cref="Commands.Success"/> instance representing a successful command execution.</returns>
    public static Success Success() =>
        new();

    /// <summary>
    /// Creates a <see cref="CommandResult"/> instance representing a "Not Found" result.
    /// </summary>
    /// <returns>A <see cref="Commands.NotFound"/> instance indicating that the target item was not found.</returns>
    public static NotFound NotFound() =>
        new();

    /// <summary>
    /// Creates a <see cref="CommandResult"/> instance representing a failed validation.
    /// </summary>
    /// <param name="validationResult">The result of the validation process, containing details about the validation errors.</param>
    /// <returns>A <see cref="Commands.ValidationFailure"/> instance carrying the provided <paramref name="validationResult"/>.</returns>
    public static ValidationFailure ValidationFailure(ValidationResult validationResult) =>
        new(validationResult);

    /// <summary>
    /// Creates a <see cref="CommandResult"/> instance representing a concurrency conflict.
    /// </summary>
    /// <returns>A <see cref="Commands.ConcurrencyConflict"/> instance indicating that a concurrency conflict was encountered while executing the command.</returns>
    public static ConcurrencyConflict ConcurrencyConflict() =>
        new();

    /// <summary>
    /// Creates a <see cref="CommandResult"/> instance representing a forbidden operation.
    /// </summary>
    /// <returns>A <see cref="Commands.Forbidden"/> instance indicating that the command execution is not allowed.</returns>
    public static Forbidden Forbidden() =>
        new();
}

/// <summary>Represents a successful command execution.</summary>
public sealed record Success : CommandResult;

/// <summary>Represents a successful command execution.</summary>
/// <typeparam name="T">The type of the result value returned by the command.</typeparam>
/// <param name="Result">The result value produced by the command.</param>
public sealed record Success<T>(T Result) : CommandResult<T>;

/// <summary>Represents a command result indicating that the target item was not found.</summary>
public sealed record NotFound : CommandResult;

/// <summary>Represents a command result indicating that the target item was not found.</summary>
/// <typeparam name="T">The type of the result value returned by the command.</typeparam>
public sealed record NotFound<T> : CommandResult<T>;

/// <summary>Represents a command result indicating that validation failed.</summary>
/// <param name="ValidationResult">The result of the validation process, containing details about the validation errors.</param>
public sealed record ValidationFailure(ValidationResult ValidationResult) : CommandResult;

/// <summary>Represents a command result indicating that validation failed.</summary>
/// <typeparam name="T">The type of the result value returned by the command.</typeparam>
/// <param name="ValidationResult">The result of the validation process, containing details about the validation errors.</param>
public sealed record ValidationFailure<T>(ValidationResult ValidationResult) : CommandResult<T>;

/// <summary>Represents a command result indicating that a concurrency conflict was encountered while executing the command.</summary>
public sealed record ConcurrencyConflict : CommandResult;
/// <summary>Represents a command result indicating that a concurrency conflict was encountered while executing the command.</summary>
/// <typeparam name="T">The type of the result value returned by the command.</typeparam>
public sealed record ConcurrencyConflict<T> : CommandResult<T>;

/// <summary>Represents a command result indicating that the command execution is not allowed.</summary>
public sealed record Forbidden : CommandResult;

/// <summary>Represents a command result indicating that the command execution is not allowed.</summary>
/// <typeparam name="T">The type of the result value returned by the command.</typeparam>
public sealed record Forbidden<T> : CommandResult<T>;