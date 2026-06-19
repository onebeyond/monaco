using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Monaco.Template.Backend.Common.Application.Commands;
using NotFound = Microsoft.AspNetCore.Http.HttpResults.NotFound;

namespace Monaco.Template.Backend.Common.Api.Application;

public static class MediatorCommandExtensions
{
	extension(ISender sender)
	{
		/// <summary>
		/// Executes the command passed and returns the corresponding response that can be either <see cref="Created{TValue}"/>, a <see cref="NotFound"/>, a <see cref="ValidationProblem"/>, or a <see cref="Conflict"/> depending on the validations and processing.
		/// </summary>
		/// <param name="command"></param>
		/// <param name="resultUri">The URI to include in the headers of the Created() response</param>
		/// <param name="uriParams">The parameters (if any) to pass for concatenating into the resultUri</param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public async Task<Results<Created<CreatedResponse>, NotFound, ValidationProblem, Conflict, ForbidHttpResult>> ExecuteCommandCreatedAsync(CommandBase<Guid> command,
																																				 string resultUri,
																																				 object[]? uriParams = null,
																																				 CancellationToken cancellationToken = default) =>
			await sender.ExecuteCommandAsync(command,
											 result => TypedResults.Created(string.Format(resultUri, [.. uriParams ?? [], result]),
																			new CreatedResponse(result)),
											 cancellationToken);

		/// <summary>
		/// Executes the command passed and returns the corresponding response that can be either <see cref="NoContent"/>, a <see cref="NotFound"/>, a <see cref="ValidationProblem"/>, or a <see cref="Conflict"/> depending on the validations and processing.
		/// </summary>
		/// <param name="command"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public async Task<Results<NoContent, NotFound, ValidationProblem, Conflict, ForbidHttpResult>> ExecuteCommandNoContentAsync(CommandBase command,
																																	CancellationToken cancellationToken = default) =>
			await sender.ExecuteCommandAsync(command, TypedResults.NoContent(), cancellationToken);

		/// <summary>
		/// Executes the command passed and returns the corresponding response that can be either <see cref="Ok"/>, a <see cref="NotFound"/>, a <see cref="ValidationProblem"/>, or a <see cref="Conflict"/> depending on the validations and processing.
		/// </summary>
		/// <param name="command"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public async Task<Results<Ok, NotFound, ValidationProblem, Conflict, ForbidHttpResult>> ExecuteCommandOkAsync(CommandBase command,
																													  CancellationToken cancellationToken = default) =>
			await sender.ExecuteCommandAsync(command, TypedResults.Ok(), cancellationToken);

		/// <summary>
		/// Executes the command passed and returns the corresponding response that can be either a user-defined <see cref="IResult"/>, a <see cref="NotFound"/>, a <see cref="ValidationProblem"/>, or a <see cref="Conflict"/> depending on the validations and processing.
		/// </summary>
		/// <typeparam name="TResponse">The type of the success response. Must implement <see cref="IResult"/>.</typeparam>
		/// <param name="command"></param>
		/// <param name="response"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public async Task<Results<TResponse, NotFound, ValidationProblem, Conflict, ForbidHttpResult>> ExecuteCommandAsync<TResponse>(CommandBase command,
																																	  TResponse response,
																																	  CancellationToken cancellationToken = default)
			where TResponse : IResult
		{
			var result = await sender.Send(command, cancellationToken);
			return result switch
			{
				Common.Application.Commands.NotFound => TypedResults.NotFound(),
				ValidationFailure validationFailed => TypedResults.ValidationProblem(validationFailed.ValidationResult.ToDictionary()),
				ConcurrencyConflict => TypedResults.Conflict(),
				Forbidden => TypedResults.Forbid(),
				Success => response,
				_ => throw new InvalidOperationException($"Unexpected command result type '{result.GetType().FullName ?? "null"}' returned for command '{command.GetType().FullName}'.")
			};
		}

		/// <summary>
		/// Executes the command passed and returns the corresponding response that can be either an <see cref="IResult"/> calculated based on a function, a <see cref="NotFound"/>, a <see cref="ValidationProblem"/>, or a <see cref="Conflict"/> depending on the validations and processing.
		/// </summary>
		/// <typeparam name="TResult">The type of the result returned by the command.</typeparam>
		/// <typeparam name="TResponse">The type of the success response. Must implement <see cref="IResult"/>.</typeparam>
		/// <param name="command"></param>
		/// <param name="func">A function to convert the result to the desired response type</param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public async Task<Results<TResponse, NotFound, ValidationProblem, Conflict, ForbidHttpResult>> ExecuteCommandAsync<TResult, TResponse>(CommandBase<TResult> command,
																																			   Func<TResult, TResponse> func,
																																			   CancellationToken cancellationToken = default)
			where TResponse : IResult
		{
			var result = await sender.Send(command, cancellationToken);
			return result switch
			{
				Common.Application.Commands.NotFound<TResult> => TypedResults.NotFound(),
				ValidationFailure<TResult> validationFailed => TypedResults.ValidationProblem(validationFailed.ValidationResult.ToDictionary()),
				ConcurrencyConflict<TResult> => TypedResults.Conflict(),
				Forbidden<TResult> => TypedResults.Forbid(),
				Success<TResult> success => func(success.Result),
				_ => throw new InvalidOperationException($"Unexpected command result type '{result.GetType().FullName ?? "null"}' returned for command '{command.GetType().FullName}'.")
			};
		}
	}
}