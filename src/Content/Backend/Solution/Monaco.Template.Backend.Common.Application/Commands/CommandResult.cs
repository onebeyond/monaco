using FluentValidation.Results;

namespace Monaco.Template.Backend.Common.Application.Commands;

/// <summary>
/// Represents the result of a command execution, including validation details, item existence, and a result value of type <typeparamref name="T"/>.
/// </summary>
/// <remarks>This type is used to encapsulate the outcome of a command, providing information about  validation
/// errors, whether the target item was found, and the result of the operation.  It includes factory methods for
/// creating instances representing success, validation failure,  or a "Not Found" result.</remarks>
/// <typeparam name="T">The type of the result value returned by the command.</typeparam>
/// <param name="ValidationResult">Indicates the result of the validation process.</param>
/// <param name="ItemNotFound">Indicates whether the target item was not found.</param>
/// <param name="Result">Indicates the result of the command execution.</param>
public record CommandResult<T>(ValidationResult ValidationResult, bool ItemNotFound, T Result)
	: CommandResult(ValidationResult, ItemNotFound)
{
	/// <summary>
	/// Creates a successful <see cref="CommandResult{T}"/> instance with the specified result.
	/// </summary>
	/// <param name="result">The result value to associate with the successful command.</param>
	/// <returns>A <see cref="CommandResult{T}"/> instance representing a successful operation with the specified result.</returns>
	public static CommandResult<T> Success(T result) =>
		new(new(), false, result);

	/// <summary>
	/// Creates a <see cref="CommandResult{T}"/> instance representing a "Not Found" result.
	/// </summary>
	/// <returns>A <see cref="CommandResult{T}"/> with a "Not Found" status and a default value of type <typeparamref name="T"/>.</returns>
	public new static CommandResult<T?> NotFound() =>
		new(new(), true, default);

	/// <summary>
	/// Creates a <see cref="CommandResult{T}"/> instance representing a failed validation.
	/// </summary>
	/// <param name="validationResult">The result of the validation process, containing details about the validation errors.</param>
	/// <param name="result">The optional result object to include in the command result. Can be <see langword="null"/>.</param>
	/// <returns>A <see cref="CommandResult{T}"/> with the validation failure details and the specified result.</returns>
	public static CommandResult<T?> ValidationFailed(ValidationResult validationResult, T? result) =>
		new(validationResult, false, result);
}

/// <summary>
/// Represents the result of a command execution, including validation outcomes and item existence status.
/// </summary>
/// <remarks>This type encapsulates the result of a command operation, providing information about validation success or
/// failure and whether a target item was found. Use the static factory methods <see cref="Success"/>, <see
/// cref="NotFound"/>, and <see cref="ValidationFailed"/> to create instances of this type.</remarks>
/// <param name="ValidationResult">Indicates the result of the validation process.</param>
/// <param name="ItemNotFound">Indicates whether the target item was not found.</param>
public record CommandResult(ValidationResult ValidationResult, bool ItemNotFound)
{
	/// <summary>
	/// Creates a <see cref="CommandResult"/> instance representing a successful operation.
	/// </summary>
	/// <returns>A <see cref="CommandResult"/> object with no errors and a success state.</returns>
	public static CommandResult Success() =>
		new(new(), false);

	/// <summary>
	/// Creates a <see cref="CommandResult"/> instance representing a "Not Found" result.
	/// </summary>
	/// <returns>A <see cref="CommandResult"/> with a "Not Found" status and an empty list of validation errors/>.</returns>
	public static CommandResult NotFound() =>
		new(new(), true);
	
	/// <summary>
	/// Creates a <see cref="CommandResult"/> instance representing a failed validation.
	/// </summary>
	/// <param name="validationResult">The result of the validation process, containing details about the validation errors.</param>
	/// <returns>A <see cref="CommandResult"/> indicating the validation failure, with the provided <paramref name="validationResult"/>.</returns>
	public static CommandResult ValidationFailed(ValidationResult validationResult) =>
		new(validationResult, false);
}